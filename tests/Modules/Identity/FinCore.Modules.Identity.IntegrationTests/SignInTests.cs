using System.Net;
using FinCore.IntegrationTests.Shared;

namespace FinCore.Modules.Identity.IntegrationTests;

/// <summary>Sign-in and identity resolution through the real authentication pipeline (spec 002 AC-1, AC-2, AC-4, AC-6).</summary>
[Trait("Category", "Integration")]
public sealed class SignInTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly FinCoreApiFactory _factory = new(postgres);
    private Tenant _tenant = null!;

    public async ValueTask InitializeAsync() => _tenant = await Tenant.CreateAsync(_factory, "SIGN");

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task First_sign_in_links_the_staff_user_and_returns_the_staff_profile() // AC-1
    {
        var (staffId, email) = await _tenant.RegisterAsync("Grace Wanjiru");
        await _tenant.GrantAsync(staffId, "LoanOfficer");

        using var client = _factory.CreateStaffClient(email);
        var me = await client.Get("/api/v1/me").OkAsync();

        me.GetProperty("kind").GetString().ShouldBe("staff");
        me.GetProperty("id").GetString().ShouldBe(staffId.ToString());
        me.GetProperty("institution").GetProperty("code").GetString().ShouldBe("SIGN");
        me.GetProperty("homeBranch").GetProperty("code").GetString().ShouldBe("001");
        me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["LoanOfficer"]);
        me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldContain("loans.apply");
        me.GetProperty("allBranches").GetBoolean().ShouldBeFalse();
        me.GetProperty("businessDate").GetString().ShouldBe("2026-10-14");
        me.GetProperty("enabledModules").EnumerateArray().Select(m => m.GetString()).ShouldContain("Identity");

        var staff = await _tenant.AdminA.Get($"/api/v1/staff/{staffId}").OkAsync();
        staff.GetProperty("status").GetString().ShouldBe("Active");
        staff.GetProperty("signInLinked").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Unregistered_or_unverified_accounts_get_no_access() // AC-2
    {
        using var stranger = _factory.CreateStaffClient("stranger@nowhere.test");
        var notRegistered = await stranger.Get("/api/v1/me");
        notRegistered.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await notRegistered.CodeAsync()).ShouldBe("Identity.StaffNotRegistered");

        var (_, email) = await _tenant.RegisterAsync("Unverified Person");
        using var unverified = _factory.CreateStaffClient(email, emailVerified: false);
        (await (await unverified.Get("/api/v1/me")).CodeAsync()).ShouldBe("Identity.EmailNotVerified");

        // Without permissions every module endpoint refuses, even though the token itself was valid.
        (await stranger.Get($"/api/v1/institutions/{_tenant.InstitutionId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Customer_tokens_are_identity_only_and_never_pass_as_staff() // AC-4
    {
        using var customer = _factory.CreateCustomerClient("customer1@example.test");

        var me = await customer.Get("/api/v1/me").OkAsync();
        me.GetProperty("kind").GetString().ShouldBe("customer");
        me.GetProperty("email").GetString().ShouldBe("customer1@example.test");

        (await customer.Get("/api/v1/approvals")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.Get($"/api/v1/institutions/{_tenant.InstitutionId}/business-date")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.Get("/api/v1/staff")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Missing_tokens_or_permissions_are_refused_in_development_too() // AC-6
    {
        using var anonymous = _factory.CreateClient();
        (await anonymous.Get("/api/v1/institutions")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var (staffId, email) = await _tenant.RegisterAsync("Field Agent");
        await _tenant.GrantAsync(staffId, "FieldOfficer");
        using var fieldOfficer = _factory.CreateStaffClient(email);
        (await fieldOfficer.Get($"/api/v1/institutions/{_tenant.InstitutionId}/branches")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
