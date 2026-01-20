using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinCore.Bff.Tests;

public sealed class GatewayTests(FakeApi api) : IClassFixture<FakeApi>
{
    [Theory] // AC-21
    [InlineData("BackOffice")]
    [InlineData("Portal")]
    public async Task Web_bff_user_is_401_and_api_requires_csrf_header(string mode)
    {
        await using var factory = new GatewayFactory(mode, api.Address);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var ct = TestContext.Current.CancellationToken;

        (await client.GetAsync("/bff/user", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var withoutCsrf = await client.GetAsync("/api/v1/institutions", ct);
        withoutCsrf.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await withoutCsrf.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString().ShouldBe("Bff.CsrfHeaderMissing");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/institutions");
        request.Headers.Add("X-CSRF", "1");
        var proxied = await client.SendAsync(request, ct);
        proxied.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await proxied.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("proxied").GetBoolean().ShouldBeTrue();
        proxied.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
    }

    [Theory]
    [InlineData("/dashboard", "/dashboard")]
    [InlineData("//evil.example", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData(null, "/")]
    public async Task Login_stub_only_redirects_to_local_paths(string? returnUrl, string expected)
    {
        await using var factory = new GatewayFactory("BackOffice", api.Address);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var url = returnUrl is null ? "/bff/login" : $"/bff/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe(expected);
        (await client.PostAsync("/bff/logout", null, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
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
