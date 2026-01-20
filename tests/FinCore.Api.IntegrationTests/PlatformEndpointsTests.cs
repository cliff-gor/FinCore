using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCore.IntegrationTests.Shared;

namespace FinCore.Api.IntegrationTests;

/// <summary>Platform endpoints and headers every client relies on (spec 001 AC-18, AC-20).</summary>
[Trait("Category", "Integration")]
public sealed class PlatformEndpointsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Version_endpoint_reports_product_contract_and_module_schema() // AC-18
    {
        await using var factory = new FinCoreApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/version", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("X-FinCore-Version").Single().ShouldBe("0.1.0");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("product").GetString().ShouldBe("0.1.0");
        body.GetProperty("commit").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("environment").GetString().ShouldBe("development");
        body.GetProperty("apiContract").GetString().ShouldBe("v1");
        body.GetProperty("minimumMobileVersion").GetString().ShouldBe("0.1.0");
        var organization = body.GetProperty("modules").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Organization");
        organization.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        organization.GetProperty("schema").GetString()!.ShouldEndWith("_Initial");
    }

    [Fact]
    public async Task Correlation_id_is_echoed_and_version_header_is_on_errors_too() // AC-20, AC-18
    {
        await using var factory = new FinCoreApiFactory(postgres);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/institutions/{Guid.NewGuid()}");
        request.Headers.Add("X-Correlation-Id", "abc");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("abc");
        response.Headers.GetValues("X-FinCore-Version").Single().ShouldBe("0.1.0");
    }

    [Fact]
    public async Task Health_endpoints_report_healthy() // AC-12 (anonymous part)
    {
        await using var factory = new FinCoreApiFactory(postgres);
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/health", "/health/live", "/health/ready" })
        {
            (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK, path);
        }
    }
}
