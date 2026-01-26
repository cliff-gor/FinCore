using System.Net;
using FinCore.IntegrationTests.Shared;
using FinCore.Modules.Organization.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Modules.Identity.IntegrationTests;

/// <summary>Institution lifecycle, branches, holidays and suspension through the API (spec 002 AC-7 to AC-10).</summary>
[Trait("Category", "Integration")]
public sealed class OrganizationAdministrationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly FinCoreApiFactory _factory = new(postgres);
    private Tenant _tenant = null!;

    public async ValueTask InitializeAsync() => _tenant = await Tenant.CreateAsync(_factory, "ORG");

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private string Institution => $"/api/v1/institutions/{_tenant.InstitutionId}";

    [Fact]
    public async Task Only_platform_admins_change_institution_status() // AC-7
    {
        (await _tenant.AdminA.Post($"{Institution}/suspend")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _tenant.AdminA.Post("/api/v1/institutions", new { code = "NOPE", name = "Nope", type = "Bank", baseCurrency = "KES", initialBusinessDate = "2026-10-14" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var reactivateFirst = await _tenant.Platform.Post($"{Institution}/reactivate");
        reactivateFirst.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await reactivateFirst.CodeAsync()).ShouldBe("Organization.InvalidStatusTransition");

        var close = await _tenant.Platform.Post($"{Institution}/close");
        close.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await close.CodeAsync()).ShouldBe("Organization.InstitutionHasActiveBranches");
    }

    [Fact]
    public async Task Branch_codes_are_unique_per_institution_and_the_hierarchy_is_shallow() // AC-8
    {
        var head = _tenant.HeadOffice;
        var child = await _tenant.AdminA.Post($"{Institution}/branches", new { code = "003", name = "Regional", parentBranchId = head })
            .OkAsync(HttpStatusCode.Created);
        child.GetProperty("parentBranchId").GetGuid().ShouldBe(head);

        var duplicate = await _tenant.AdminA.Post($"{Institution}/branches", new { code = "003", name = "Duplicate" });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.CodeAsync()).ShouldBe("Organization.BranchCodeTaken");

        // Concurrent duplicates: the unique index lets exactly one through.
        var race = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            _tenant.AdminA.Post($"{Institution}/branches", new { code = "009", name = "Race" })));
        race.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        race.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);

        (await _tenant.AdminA.Post($"{Institution}/branches", new { code = "004", name = "Deep", parentBranchId = child.Id() }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _tenant.AdminA.Post($"{Institution}/branches", new { code = "4", name = "Short" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Another institution's branch can never be a parent.
        var other = await Tenant.CreateAsync(_factory, "OTHR");
        (await _tenant.AdminA.Post($"{Institution}/branches", new { code = "005", name = "Cross", parentBranchId = other.HeadOffice }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Holiday_calendar_drives_working_days_and_past_holidays_are_immutable() // AC-9
    {
        await _tenant.AdminA.Put($"{Institution}/holidays", new
        {
            weekendDays = new[] { "Friday", "Saturday" },
            holidays = new[]
            {
                new { date = "2026-10-20", name = "Mashujaa Day" },
                new { date = "2026-12-12", name = "Jamhuri Day" },
                new { date = "2026-12-25", name = "Christmas Day" },
            },
        }).OkAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var calendar = scope.ServiceProvider.GetRequiredService<IHolidayCalendar>();
        var ct = TestContext.Current.CancellationToken;
        (await calendar.IsWorkingDayAsync(_tenant.InstitutionId, new DateOnly(2026, 10, 16), ct)).ShouldBeFalse(); // Friday
        (await calendar.IsWorkingDayAsync(_tenant.InstitutionId, new DateOnly(2026, 10, 18), ct)).ShouldBeTrue();  // Sunday
        (await calendar.NextWorkingDayAsync(_tenant.InstitutionId, new DateOnly(2026, 10, 19), ct)).ShouldBe(new DateOnly(2026, 10, 21));

        var past = await _tenant.AdminA.Put($"{Institution}/holidays", new
        {
            weekendDays = new[] { "Saturday", "Sunday" },
            holidays = new[] { new { date = "2026-01-01", name = "New Year" } },
        });
        past.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var listed = await _tenant.AdminA.Get($"{Institution}/holidays?year=2026").OkAsync();
        listed.GetProperty("holidays").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Suspension_blocks_writes_but_not_reads_and_closure_blocks_sign_in() // AC-10
    {
        await _tenant.Platform.Post($"{Institution}/suspend").OkAsync();

        // Identity learns about the suspension through the outbox; wait until the write guard sees it.
        var blocked = await Eventually.HoldsAsync(async () =>
        {
            var write = await _tenant.AdminA.Post($"{Institution}/branches", new { code = "006", name = "Blocked" });
            return write.StatusCode == HttpStatusCode.Forbidden && await write.CodeAsync() == "Organization.InstitutionSuspended";
        }, TimeSpan.FromSeconds(10));
        blocked.ShouldBeTrue();
        await _tenant.AdminA.Get($"{Institution}/branches").OkAsync();

        await _tenant.Platform.Post($"{Institution}/reactivate").OkAsync();
        (await Eventually.HoldsAsync(async () =>
            (await _tenant.AdminA.Post($"{Institution}/branches", new { code = "007", name = "Allowed" })).StatusCode == HttpStatusCode.Created,
            TimeSpan.FromSeconds(10))).ShouldBeTrue();

        // Closing needs every branch closed, which in turn needs their staff deactivated first.
        var closer = await Tenant.CreateAsync(_factory, "CLSD");
        var staffAtHead = new[] { closer.AdminAId, closer.AdminBId };
        foreach (var id in staffAtHead)
        {
            await closer.Platform.Post($"/api/v1/staff/{id}/deactivate").OkAsync();
        }

        var branches = await closer.Platform.Get($"/api/v1/institutions/{closer.InstitutionId}/branches").OkAsync();
        foreach (var branch in branches.GetProperty("items").EnumerateArray())
        {
            await closer.Platform.Post($"/api/v1/institutions/{closer.InstitutionId}/branches/{branch.Id()}/close").OkAsync();
        }

        await closer.Platform.Post($"/api/v1/institutions/{closer.InstitutionId}/close").OkAsync();
        await closer.Platform.Post($"/api/v1/staff/{closer.AdminAId}/reactivate").OkAsync();

        // Even a reactivated user of a Closed institution cannot use FinCore.
        (await Eventually.HoldsAsync(async () =>
            await (await closer.AdminA.Get("/api/v1/me")).CodeAsync() == "Identity.StaffInactive",
            TimeSpan.FromSeconds(10))).ShouldBeTrue();
    }
}
