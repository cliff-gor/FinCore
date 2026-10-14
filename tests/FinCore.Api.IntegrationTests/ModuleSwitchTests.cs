using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.IntegrationTests.Shared;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Api.IntegrationTests;

/// <summary>A switched-off module keeps its schema but exposes nothing: no routes, no subscribers (spec 001 AC-19).</summary>
[Trait("Category", "Integration")]
public sealed class ModuleSwitchTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Disabled_module_has_no_routes_or_subscriptions_and_is_reported_disabled() // AC-19
    {
        await using var factory = new FinCoreApiFactory(
            postgres,
            settings: new Dictionary<string, string?> { ["Modules:Organization:Enabled"] = "false" });
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        (await client.GetAsync("/api/v1/institutions", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Organization's own subscriber is gone; Identity (still enabled) keeps listening to Organization's events.
        using var scope = factory.Services.CreateScope();
        var organizationSubscriber = Type.GetType(
            "FinCore.Modules.Organization.Application.IntegrationEventHandlers.InstitutionCreatedLoggingHandler, FinCore.Modules.Organization.Application",
            throwOnError: true)!;
        scope.ServiceProvider.GetService(organizationSubscriber).ShouldBeNull();

        var version = await client.GetFromJsonAsync<JsonElement>("/api/version", ct);
        var organization = version.GetProperty("modules").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Organization");
        organization.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        organization.GetProperty("schema").GetString().ShouldNotBeNull(); // migrations still ran
    }
}
