using System.Net;
using FinCore.IntegrationTests.Shared;

namespace FinCore.Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ProductionAuthorizationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Outside_development_module_endpoints_fail_closed_but_platform_endpoints_are_open() // AC-12
    {
        await using var factory = new FinCoreApiFactory(postgres, environment: "Production");
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        (await client.GetAsync("/api/v1/institutions", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/v1/institutions/{Guid.NewGuid()}", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/v1/institutions/{Guid.NewGuid()}/business-date", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var create = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        (await client.PostAsync("/api/v1/institutions", create, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await client.GetAsync("/health/live", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/version", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
