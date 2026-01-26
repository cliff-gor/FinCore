using System.Net;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.IntegrationTests.Shared;
using FinCore.Modules.Identity.Application.Approvals;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Modules.Identity.IntegrationTests;

/// <summary>The maker-checker engine end to end (spec 002 AC-11 to AC-18).</summary>
[Trait("Category", "Integration")]
public sealed class MakerCheckerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly FinCoreApiFactory _factory = new(postgres);
    private Tenant _tenant = null!;

    public async ValueTask InitializeAsync() => _tenant = await Tenant.CreateAsync(_factory, "MKCK");

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Role_grant_waits_for_a_second_admin_and_applies_on_the_next_request() // AC-11, AC-12
    {
        var (userId, email) = await _tenant.RegisterAsync("Loan Person");
        using var user = _factory.CreateStaffClient(email);
        (await user.Get("/api/v1/me").OkAsync()).GetProperty("roles").GetArrayLength().ShouldBe(0);

        var request = await _tenant.AdminA.Post($"/api/v1/staff/{userId}/roles", new { role = "LoanOfficer" }).OkAsync(HttpStatusCode.Accepted);
        request.GetProperty("status").GetString().ShouldBe("Pending");
        (await user.Get("/api/v1/me").OkAsync()).GetProperty("roles").GetArrayLength().ShouldBe(0);

        var ownApproval = await _tenant.AdminA.Post($"/api/v1/approvals/{request.Id()}/approve");
        ownApproval.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ownApproval.CodeAsync()).ShouldBe("Identity.FourEyesViolation");

        var approved = await _tenant.AdminB.Post($"/api/v1/approvals/{request.Id()}/approve", new { comment = "Fine" }).OkAsync();
        approved.GetProperty("status").GetString().ShouldBe("Approved");
        approved.GetProperty("decisions")[0].GetProperty("comment").GetString().ShouldBe("Fine");

        // The cached access profile was cleared on approval, so the very next request carries the new role.
        (await user.Get("/api/v1/me").OkAsync()).GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["LoanOfficer"]);
    }

    [Fact]
    public async Task Limit_raises_respect_checker_authority_and_the_two_checker_threshold() // AC-13
    {
        var (adminC, _) = await _tenant.RegisterAsync("Admin C");
        await _tenant.GrantAsync(adminC, "SystemAdmin");
        using var clientC = _factory.CreateStaffClient("admin.c@mkck.test");
        var (adminD, _) = await _tenant.RegisterAsync("Admin D");
        await _tenant.GrantAsync(adminD, "SystemAdmin");
        using var clientD = _factory.CreateStaffClient("admin.d@mkck.test");

        // Give B 200,000 and C and D 5,000,000 of loan authority. The PlatformAdmin has no limits and its approval
        // completes a request on its own, even above the two-checker threshold (spec 002 owner decision).
        await RaiseAsync(_tenant.AdminBId, 200_000m, _tenant.Platform);
        await RaiseAsync(adminC, 5_000_000m, _tenant.Platform);
        await RaiseAsync(adminD, 5_000_000m, _tenant.Platform);

        var (userId, _) = await _tenant.RegisterAsync("Credit Person");
        var toHalfMillion = await RequestRaiseAsync(userId, 500_000m);
        var refused = await _tenant.AdminB.Post($"/api/v1/approvals/{toHalfMillion}/approve");
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.CodeAsync()).ShouldBe("Identity.ApprovalLimitExceeded");
        await clientC.Post($"/api/v1/approvals/{toHalfMillion}/approve").OkAsync();

        // Above 1,000,000 two distinct eligible checkers are needed.
        var toTwoMillion = await RequestRaiseAsync(userId, 2_000_000m);
        (await clientC.Post($"/api/v1/approvals/{toTwoMillion}/approve").OkAsync()).GetProperty("status").GetString().ShouldBe("Pending");
        var again = await clientC.Post($"/api/v1/approvals/{toTwoMillion}/approve");
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict); // the same person can't count twice
        (await again.CodeAsync()).ShouldBe("Identity.AlreadyDecidedByChecker");
        (await clientD.Post($"/api/v1/approvals/{toTwoMillion}/approve").OkAsync()).GetProperty("status").GetString().ShouldBe("Approved");

        var staff = await _tenant.AdminA.Get($"/api/v1/staff/{userId}").OkAsync();
        staff.GetProperty("approvalLimits")[0].GetProperty("maxAmount").GetDecimal().ShouldBe(2_000_000m);

        // One rejection rejects; a reason is mandatory.
        var toThreeMillion = await RequestRaiseAsync(userId, 3_000_000m);
        (await clientC.Post($"/api/v1/approvals/{toThreeMillion}/reject", new { reason = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var rejected = await clientC.Post($"/api/v1/approvals/{toThreeMillion}/reject", new { reason = "Too high for this role" }).OkAsync();
        rejected.GetProperty("status").GetString().ShouldBe("Rejected");
        rejected.GetProperty("outcomeReason").GetString().ShouldBe("Too high for this role");

        // Lowering applies immediately with 200 and no approval.
        var lowered = await _tenant.AdminA.Put($"/api/v1/staff/{userId}/approval-limits/LoanApproval", new { maxAmount = 100_000m }).OkAsync();
        lowered.GetProperty("applied").GetProperty("approvalLimits")[0].GetProperty("maxAmount").GetDecimal().ShouldBe(100_000m);
    }

    [Fact]
    public async Task Requests_can_be_withdrawn_and_expire_after_seven_days() // AC-14
    {
        var (userId, _) = await _tenant.RegisterAsync("Expiring Person");
        var withdrawn = await _tenant.AdminA.Post($"/api/v1/staff/{userId}/roles", new { role = "Auditor" }).OkAsync(HttpStatusCode.Accepted);
        (await _tenant.AdminB.Post($"/api/v1/approvals/{withdrawn.Id()}/withdraw")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _tenant.AdminA.Post($"/api/v1/approvals/{withdrawn.Id()}/withdraw").OkAsync()).GetProperty("status").GetString().ShouldBe("Withdrawn");

        var expiring = await _tenant.AdminA.Post($"/api/v1/staff/{userId}/roles", new { role = "Accountant" }).OkAsync(HttpStatusCode.Accepted);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // Move the request 8 days into the past instead of waiting a week.
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.ApprovalRequests.Where(r => r.Id == new ApprovalRequestId(expiring.Id()))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAtUtc, DateTime.UtcNow.AddDays(-1)), TestContext.Current.CancellationToken);
            var expired = await scope.ServiceProvider.GetRequiredService<IDispatcher>()
                .Send(new ExpireApprovalRequestsCommand(), TestContext.Current.CancellationToken);
            expired.Value.ShouldBe(1);
        }

        var late = await _tenant.AdminB.Post($"/api/v1/approvals/{expiring.Id()}/approve");
        late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await late.CodeAsync()).ShouldBe("Identity.ApprovalExpired");
    }

    [Fact]
    public async Task Concurrent_decisions_have_exactly_one_winner() // AC-15
    {
        var (adminC, _) = await _tenant.RegisterAsync("Racer C");
        await _tenant.GrantAsync(adminC, "SystemAdmin");
        using var clientC = _factory.CreateStaffClient("racer.c@mkck.test");
        await clientC.Get("/api/v1/me").OkAsync(); // link the account before the race

        var (userId, _) = await _tenant.RegisterAsync("Contested Person");
        var request = await _tenant.AdminA.Post($"/api/v1/staff/{userId}/roles", new { role = "CreditApprover" }).OkAsync(HttpStatusCode.Accepted);

        var results = await Task.WhenAll(
            _tenant.AdminB.Post($"/api/v1/approvals/{request.Id()}/approve"),
            clientC.Post($"/api/v1/approvals/{request.Id()}/approve"));

        results.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        var loser = results.Single(r => r.StatusCode != HttpStatusCode.OK);
        loser.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await loser.CodeAsync()).ShouldBe("Identity.ApprovalAlreadyDecided");
    }

    [Fact]
    public async Task Approval_of_a_no_longer_applicable_request_fails_without_changes() // AC-16
    {
        var (userId, _) = await _tenant.RegisterAsync("Leaving Person");
        var request = await _tenant.AdminA.Post($"/api/v1/staff/{userId}/roles", new { role = "Accountant" }).OkAsync(HttpStatusCode.Accepted);
        await _tenant.AdminA.Post($"/api/v1/staff/{userId}/deactivate").OkAsync();

        var outcome = await _tenant.AdminB.Post($"/api/v1/approvals/{request.Id()}/approve").OkAsync();

        outcome.GetProperty("status").GetString().ShouldBe("Failed");
        outcome.GetProperty("outcomeReason").GetString().ShouldBe("Identity.StaffInactive");
        (await _tenant.AdminA.Get($"/api/v1/staff/{userId}").OkAsync()).GetProperty("roles").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Removing_access_is_immediate() // AC-17
    {
        var (userId, email) = await _tenant.RegisterAsync("Auditor Person");
        await _tenant.GrantAsync(userId, "Auditor");
        using var user = _factory.CreateStaffClient(email);
        await user.Get($"/api/v1/institutions/{_tenant.InstitutionId}/branches").OkAsync();

        await _tenant.AdminA.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/staff/{userId}/roles/Auditor"), TestContext.Current.CancellationToken);
        (await user.Get($"/api/v1/institutions/{_tenant.InstitutionId}/branches")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await _tenant.AdminA.Post($"/api/v1/staff/{userId}/deactivate").OkAsync();
        (await (await user.Get("/api/v1/me")).CodeAsync()).ShouldBe("Identity.StaffInactive");
    }

    [Fact]
    public async Task Inbox_shows_only_requests_the_checker_may_decide() // AC-18
    {
        var (userId, _) = await _tenant.RegisterAsync("Inbox Person");
        var decidable = await _tenant.AdminA.Post($"/api/v1/staff/{userId}/roles", new { role = "FieldOfficer" }).OkAsync(HttpStatusCode.Accepted);
        var ownRequest = await _tenant.AdminB.Post($"/api/v1/staff/{userId}/roles", new { role = "BranchOfficer" }).OkAsync(HttpStatusCode.Accepted);
        var aboutB = await _tenant.AdminA.Post($"/api/v1/staff/{_tenant.AdminBId}/roles", new { role = "Auditor" }).OkAsync(HttpStatusCode.Accepted);
        var aboveLimit = await RequestRaiseAsync(userId, 50_000m); // B has no loan authority

        var inbox = await _tenant.AdminB.Get("/api/v1/approvals?status=Pending").OkAsync();
        var ids = inbox.EnumerateArray().Select(r => r.Id()).ToList();
        ids.ShouldContain(decidable.Id());
        ids.ShouldNotContain(ownRequest.Id());
        ids.ShouldNotContain(aboutB.Id());
        ids.ShouldNotContain(aboveLimit);

        var mine = await _tenant.AdminB.Get("/api/v1/approvals?mine=true").OkAsync();
        mine.EnumerateArray().Select(r => r.Id()).ShouldContain(ownRequest.Id());
    }

    private async Task<Guid> RequestRaiseAsync(Guid staffId, decimal amount) =>
        (await _tenant.AdminA.Put($"/api/v1/staff/{staffId}/approval-limits/LoanApproval", new { maxAmount = amount })
            .OkAsync(HttpStatusCode.Accepted)).GetProperty("pendingApproval").Id();

    private async Task RaiseAsync(Guid staffId, decimal amount, HttpClient checker) =>
        await checker.Post($"/api/v1/approvals/{await RequestRaiseAsync(staffId, amount)}/approve").OkAsync();
}
