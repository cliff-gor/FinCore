using System.Collections.Concurrent;
using System.Net;
using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.BuildingBlocks.Infrastructure.Idempotency;
using FinCore.IntegrationTests.Shared;
using FinCore.Modules.Identity.Contracts.IntegrationEvents;
using FinCore.Modules.Identity.Infrastructure.Persistence;
using FinCore.Modules.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Modules.Identity.IntegrationTests;

/// <summary>Cross-module approvals, isolation, audit, seed data and the M1 exit scenario (spec 002 AC-19 to AC-23).</summary>
[Trait("Category", "Integration")]
public sealed class PlatformScenarioTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Other_modules_get_approvals_through_events_both_ways() // AC-20
    {
        await using var factory = new FinCoreApiFactory(
            postgres,
            settings: new Dictionary<string, string?> { ["Approvals:Operations:Test.Operation:Permission"] = "identity.approve" },
            configureServices: s => s.AddIntegrationEventHandler<ApprovalRequestDecidedIntegrationEvent, DecisionRecorder, IdentityDbContext>());
        var tenant = await Tenant.CreateAsync(factory, "XMOD");
        var entityId = Guid.NewGuid().ToString();

        // A test-only module asks for approval exactly like Ledger will in M3.
        await factory.Services.GetRequiredService<IEventBus>().PublishAsync(
            new ApprovalRequestedIntegrationEvent(Guid.CreateVersion7(), DateTime.UtcNow, tenant.InstitutionId, "Test", "Test.Operation",
                entityId, null, null, """{"what":"test"}""", tenant.AdminAId),
            Ct);

        var inbox = await tenant.AdminB.Get("/api/v1/approvals?status=Pending").OkAsync();
        var request = inbox.EnumerateArray().Single(r => r.GetProperty("entityId").GetString() == entityId);
        await tenant.AdminB.Post($"/api/v1/approvals/{request.Id()}/approve").OkAsync();

        // The decision travels back through the outbox and is handled exactly once (inbox).
        (await Eventually.HoldsAsync(() => Task.FromResult(DecisionRecorder.Outcomes.ContainsKey(entityId)), TimeSpan.FromSeconds(10))).ShouldBeTrue();
        DecisionRecorder.Outcomes[entityId].ShouldBe("Approved");
        DecisionRecorder.Calls[entityId].ShouldBe(1);
    }

    [Fact]
    public async Task Staff_never_see_another_institution_and_branch_scope_filters_listings() // AC-19
    {
        await using var factory = new FinCoreApiFactory(postgres);
        var first = await Tenant.CreateAsync(factory, "ISOA");
        var second = await Tenant.CreateAsync(factory, "ISOB");

        (await first.AdminA.Get($"/api/v1/institutions/{second.InstitutionId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await first.AdminA.Get($"/api/v1/institutions/{second.InstitutionId}/branches")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var institutions = await first.AdminA.Get("/api/v1/institutions").OkAsync();
        institutions.GetProperty("items").EnumerateArray().Select(i => i.Id()).ShouldBe([first.InstitutionId]);
        (await first.AdminA.Get($"/api/v1/staff/{second.AdminAId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // SystemAdmins hold all-branch scope, so they see staff of every branch; PlatformAdmin sees every institution.
        await first.RegisterAsync("City Staff", first.CityBranch);
        var staff = await first.AdminA.Get("/api/v1/staff").OkAsync();
        staff.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("homeBranchId").GetGuid()).Distinct().Count().ShouldBe(2);
        var all = await first.Platform.Get("/api/v1/institutions?pageSize=100").OkAsync();
        all.GetProperty("totalCount").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Every_successful_command_is_audited_without_its_payload() // AC-21
    {
        await using var factory = new FinCoreApiFactory(postgres);
        var tenant = await Tenant.CreateAsync(factory, "AUDT");
        var (staffId, _) = await tenant.RegisterAsync("Audited Person");

        await using var scope = factory.Services.CreateAsyncScope();
        var audit = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().AuditLog.AsNoTracking()
            .Where(a => a.InstitutionId == tenant.InstitutionId)
            .ToListAsync(Ct);

        var registration = audit.Single(a => a.Command == "RegisterStaffCommand" && a.EntityId == "audited.person@audt.test");
        registration.UserId.ShouldBe(TestTokens.SubjectFor("admin.a@audt.test"));
        registration.RequestHash.Length.ShouldBe(64);
        registration.CorrelationId.ShouldNotBeNullOrEmpty();
        audit.ShouldContain(a => a.Command == "RequestRoleGrantCommand");
        audit.ShouldNotContain(a => a.Command.EndsWith("Query", StringComparison.Ordinal)); // reads are not audited
        _ = staffId;
    }

    [Fact]
    public async Task Development_seed_creates_every_persona_idempotently() // AC-22
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:SeedDevData"] = "true",
            ["ConnectionStrings:fincore"] = postgres.NewDatabaseConnectionString(),
        };

        for (var run = 0; run < 2; run++)
        {
            await using var factory = new FinCoreApiFactory(postgres, settings: settings);
            _ = factory.Server;
            await using var scope = factory.Services.CreateAsyncScope();
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var organization = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();

            (await identity.StaffUsers.CountAsync(Ct)).ShouldBe(19); // 1 PlatformAdmin + 9 per institution
            (await organization.Branches.CountAsync(Ct)).ShouldBe(4);
            (await organization.HolidayCalendars.CountAsync(Ct)).ShouldBe(2);

            // A seeded admin signs in with the realm's email and gets the seeded role.
            using var admin = factory.CreateStaffClient("admin1.fcb@fincore.local");
            var me = await admin.Get("/api/v1/me").OkAsync();
            me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["SystemAdmin"]);
            me.GetProperty("institution").GetProperty("code").GetString().ShouldBe("FCB");
        }
    }

    [Fact]
    public async Task M1_exit_scenario_works_end_to_end() // AC-23
    {
        await using var factory = new FinCoreApiFactory(postgres);
        var platform = await factory.CreatePlatformAdminClientAsync();

        foreach (var (code, type) in new[] { ("EXBK", "Bank"), ("EXMF", "Mfi") })
        {
            // PlatformAdmin creates the institution and its first branch, then bootstraps its first SystemAdmin.
            var institution = await platform.Post("/api/v1/institutions", new
            {
                code, name = $"{code} Ltd", type, baseCurrency = "KES", initialBusinessDate = "2026-10-14",
            }).OkAsync(HttpStatusCode.Created);
            var hq = await platform.Post($"/api/v1/institutions/{institution.Id()}/branches", new { code = "001", name = "HQ" })
                .OkAsync(HttpStatusCode.Created);
            var lower = code.ToLowerInvariant();
            await platform.Post("/api/v1/staff", new
            {
                email = $"first.admin@{lower}.test", name = "First Admin", homeBranchId = hq.Id(), institutionId = institution.Id(), bootstrapSystemAdmin = true,
            }).OkAsync(HttpStatusCode.Created);

            // The SystemAdmin builds the institution: another branch, a second admin, an officer.
            using var admin = factory.CreateStaffClient($"first.admin@{lower}.test");
            var branch = await admin.Post($"/api/v1/institutions/{institution.Id()}/branches", new { code = "002", name = "Market Branch" })
                .OkAsync(HttpStatusCode.Created);
            var second = await admin.Post("/api/v1/staff", new { email = $"second.admin@{lower}.test", name = "Second Admin", homeBranchId = hq.Id() })
                .OkAsync(HttpStatusCode.Created);
            var officer = await admin.Post("/api/v1/staff", new { email = $"officer@{lower}.test", name = "Branch Officer", homeBranchId = branch.Id() })
                .OkAsync(HttpStatusCode.Created);

            // Maker-checker: the second admin's role is approved by the PlatformAdmin, then the officer's by the second admin.
            var adminGrant = await admin.Post($"/api/v1/staff/{second.Id()}/roles", new { role = "SystemAdmin" }).OkAsync(HttpStatusCode.Accepted);
            await platform.Post($"/api/v1/approvals/{adminGrant.Id()}/approve").OkAsync();
            using var secondAdmin = factory.CreateStaffClient($"second.admin@{lower}.test");
            var officerGrant = await admin.Post($"/api/v1/staff/{officer.Id()}/roles", new { role = "BranchOfficer" }).OkAsync(HttpStatusCode.Accepted);
            await secondAdmin.Post($"/api/v1/approvals/{officerGrant.Id()}/approve").OkAsync();

            // The new role works on the officer's first request.
            using var officerClient = factory.CreateStaffClient($"officer@{lower}.test");
            var me = await officerClient.Get("/api/v1/me").OkAsync();
            me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["BranchOfficer"]);
            me.GetProperty("homeBranch").GetProperty("code").GetString().ShouldBe("002");
            me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldContain("accounts.open");
        }
    }

    internal sealed class DecisionRecorder : IIntegrationEventHandler<ApprovalRequestDecidedIntegrationEvent>
    {
        public static readonly ConcurrentDictionary<string, string> Outcomes = new();
        public static readonly ConcurrentDictionary<string, int> Calls = new();

        public Task Handle(ApprovalRequestDecidedIntegrationEvent e, CancellationToken cancellationToken)
        {
            Outcomes[e.EntityId] = e.Outcome;
            Calls.AddOrUpdate(e.EntityId, 1, (_, n) => n + 1);
            return Task.CompletedTask;
        }
    }
}
