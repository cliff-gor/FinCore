using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCore.Bff.Sessions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinCore.Bff.Tests;

/// <summary>
/// Gateway behaviour in each mode against a real HTTP stand-in for the API, so YARP proxying is exercised end to end
/// (spec 001 AC-21, AC-22; spec 002 "BFF (Back Office, Portal)", AC-3).
/// </summary>
public sealed class GatewayTests(FakeApi api) : IClassFixture<FakeApi>
{
    private static readonly WebApplicationFactoryClientOptions NoRedirectsNoCookies = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = false,
    };

    [Theory] // AC-21
    [InlineData("BackOffice")]
    [InlineData("Portal")]
    public async Task Web_bff_user_is_401_and_api_requires_csrf_header(string mode)
    {
        await using var factory = new GatewayFactory(mode, api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        var ct = TestContext.Current.CancellationToken;

        var user = await client.GetAsync("/bff/user", ct);
        user.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await user.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString().ShouldBe("Bff.NotSignedIn");

        var withoutCsrf = await client.GetAsync("/api/v1/institutions", ct);
        withoutCsrf.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await withoutCsrf.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString().ShouldBe("Bff.CsrfHeaderMissing");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/institutions");
        request.Headers.Add("X-CSRF", "1");
        var proxied = await client.SendAsync(request, ct);
        proxied.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await proxied.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("proxied").GetBoolean().ShouldBeTrue();
        // No session: proxied without a token, so the API itself answers 401 for protected endpoints.
        body.GetProperty("authorization").GetString().ShouldBeEmpty();
        proxied.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
    }

    [Theory]
    [InlineData("/dashboard", "/dashboard")]
    [InlineData("//evil.example", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData(null, "/")]
    public async Task Login_challenges_the_identity_provider_with_pkce_and_a_local_return_url(string? returnUrl, string expected)
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);

        var url = returnUrl is null ? "/bff/login" : $"/bff/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.ShouldStartWith($"{api.Address.TrimEnd('/')}/idp/auth?");
        location.ShouldContain("client_id=fincore-backoffice");
        location.ShouldContain("response_type=code");
        location.ShouldContain("code_challenge_method=S256");
        factory.ChallengeReturnUrls.ShouldHaveSingleItem().ShouldBe(expected);

        // The IdP returns with a cross-site form POST, so the correlation cookie must be SameSite=None (+Secure).
        var correlation = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Correlation", StringComparison.Ordinal));
        correlation.ShouldContain("samesite=none", Case.Insensitive);
        correlation.ShouldContain("secure", Case.Insensitive);
        correlation.ShouldContain("httponly", Case.Insensitive);
    }

    [Fact] // spec 002 NFR: /bff/login is limited to 10 requests per minute per IP
    public async Task Login_is_rate_limited()
    {
        await using var factory = new GatewayFactory("Portal", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 10; i++)
        {
            (await client.GetAsync("/bff/login", ct)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        (await client.GetAsync("/bff/login", ct)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Theory] // AC-3
    [InlineData("BackOffice", "__Host-fincore-backoffice")]
    [InlineData("Portal", "__Host-fincore-portal")]
    public async Task Session_cookie_is_host_prefixed_http_only_secure_and_strict(string mode, string cookieName)
    {
        await using var factory = new GatewayFactory(mode, api.Address);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BffAuthentication.CookieScheme);

        options.Cookie.Name.ShouldBe(cookieName);
        options.Cookie.HttpOnly.ShouldBeTrue();
        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
        options.Cookie.SameSite.ShouldBe(SameSiteMode.Strict);
        options.Cookie.Path.ShouldBe("/");
        options.Cookie.Domain.ShouldBeNull();
        // The cookie carries only a session id; the tokens are in the server-side store.
        options.SessionStore.ShouldBeOfType<BffTicketStore>();
    }

    [Fact] // AC-1 shape, AC-4: /bff/user is the API's /me for the session's access token
    public async Task Bff_user_returns_the_me_shape_for_a_signed_in_session()
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        request.Headers.Add("Cookie", await factory.SignInAsync());

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        user.GetProperty("kind").GetString().ShouldBe("staff");
        user.GetProperty("name").GetString().ShouldBe("Admin One");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact] // AC-2: the API's refusal reaches the app unchanged
    public async Task Bff_user_passes_through_the_not_registered_problem()
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        request.Headers.Add("Cookie", await factory.SignInAsync(FakeApi.UnregisteredAccessToken));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("code").GetString().ShouldBe("Identity.StaffNotRegistered");
    }

    [Fact] // AC-3: a revoked or expired server-side session is signed out
    public async Task Bff_user_is_401_once_the_session_is_revoked()
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        var cookie = await factory.SignInAsync();
        factory.Sessions.Revoke(factory.Sessions.All.Single().Id, DateTime.UtcNow);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        factory.Sessions.All.ShouldBeEmpty();
    }

    [Fact] // AC-3: tokens never reach the browser; the BFF attaches them
    public async Task Api_calls_carry_the_session_token_but_never_the_cookie_or_a_browser_token()
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/institutions");
        request.Headers.Add("Cookie", await factory.SignInAsync());
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add("Authorization", "Bearer forged-by-script");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var seen = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        seen.GetProperty("authorization").GetString().ShouldBe("Bearer access-1");
        seen.GetProperty("cookie").GetString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Expiring_access_token_is_refreshed_before_proxying()
    {
        await using var factory = new GatewayFactory("Portal", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/institutions");
        request.Headers.Add("Cookie", await factory.SignInAsync(accessTokenExpiresIn: TimeSpan.FromSeconds(30)));
        request.Headers.Add("X-CSRF", "1");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("authorization").GetString().ShouldBe("Bearer access-2");
    }

    [Fact]
    public async Task Logout_requires_the_csrf_header()
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/bff/logout");
        request.Headers.Add("Cookie", await factory.SignInAsync());

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        factory.Sessions.All.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_ends_the_session_and_returns_the_end_session_url()
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(NoRedirectsNoCookies);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/bff/logout");
        request.Headers.Add("Cookie", await factory.SignInAsync());
        request.Headers.Add("X-CSRF", "1");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var logoutUrl = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("logoutUrl").GetString()!;
        logoutUrl.ShouldStartWith($"{api.Address.TrimEnd('/')}/idp/logout?");
        logoutUrl.ShouldContain("id_token_hint=id-token-1");
        logoutUrl.ShouldContain($"post_logout_redirect_uri={Uri.EscapeDataString("http://localhost/")}");
        api.RevokedTokens.ShouldContain("refresh-1");
        factory.Sessions.All.ShouldBeEmpty();

        // The cookie is expired with the same attributes it was issued with.
        var deleted = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(factory.CookieName, StringComparison.Ordinal));
        deleted.ShouldContain("expires=Thu, 01 Jan 1970", Case.Insensitive);
        deleted.ShouldContain("path=/", Case.Insensitive);
        deleted.ShouldContain("secure", Case.Insensitive);
        deleted.ShouldContain("samesite=strict", Case.Insensitive);
        deleted.ShouldContain("httponly", Case.Insensitive);
    }

    [Theory] // AC-22
    [InlineData("mobile/0.0.9 (1)", HttpStatusCode.UpgradeRequired)]
    [InlineData("mobile/0.1.0 (1)", HttpStatusCode.OK)]
    [InlineData("mobile/1.2.0", HttpStatusCode.OK)]
    [InlineData("garbage", HttpStatusCode.UpgradeRequired)]
    public async Task Mobile_gateway_enforces_minimum_app_version(string appVersion, HttpStatusCode expected)
    {
        await using var factory = new GatewayFactory("Mobile", api.Address);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-App-Version", appVersion);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
        if (expected == HttpStatusCode.UpgradeRequired)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            problem.GetProperty("code").GetString().ShouldBe("Gateway.UpgradeRequired");
            problem.GetProperty("minimumVersion").GetString().ShouldBe("0.1.0");
        }
    }

    [Fact]
    public async Task Mobile_gateway_has_no_bff_session_endpoints_or_csrf()
    {
        await using var factory = new GatewayFactory("Mobile", api.Address);
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        (await client.GetAsync("/bff/user", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/v1/institutions", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/gateway/health", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
