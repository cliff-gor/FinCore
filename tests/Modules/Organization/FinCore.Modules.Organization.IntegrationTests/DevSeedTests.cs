using System.Net.Http.Json;
using FinCore.IntegrationTests.Shared;
using FinCore.Modules.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Modules.Organization.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DevSeedTests(PostgresFixture postgres)
{
    private static readonly Dictionary<string, string?> SeedOn = new() { ["Database:SeedDevData"] = "true" };

    [Fact]
    public async Task Startup_migrates_seeds_currencies_and_institutions_idempotently() // AC-13
    {
        var connectionString = postgres.NewDatabaseConnectionString();
        var settings = new Dictionary<string, string?>(SeedOn) { ["ConnectionStrings:fincore"] = connectionString };

        // First start on an empty database.
        await using (var first = new FinCoreApiFactory(postgres, settings: settings))
        {
            using var client = first.CreateClient();
            await AssertSeededAsync(first, client);
        }

        // Restart against the same database: the seeder must skip existing codes rather than fail or duplicate.
        await using var second = new FinCoreApiFactory(postgres, settings: settings);
        using var secondClient = second.CreateClient();
        await AssertSeededAsync(second, secondClient);
    }

    private static async Task AssertSeededAsync(FinCoreApiFactory factory, HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PageDto>(InstitutionApi.Path, InstitutionApi.Json, TestContext.Current.CancellationToken);
        page!.TotalCount.ShouldBe(2);
        page.Items.Select(i => (i.Code, i.Type, i.BaseCurrency)).ShouldBe([("FCB", "Bank", "KES"), ("FCMFI", "Mfi", "KES")]);
        page.Items.ShouldAllBe(i => i.CurrentBusinessDate == new DateOnly(2026, 10, 13));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var currencies = await db.Currencies.OrderBy(c => c.IsoCode).ToListAsync(TestContext.Current.CancellationToken);
        currencies.Select(c => c.IsoCode).ShouldBe(["EUR", "GBP", "KES", "TZS", "UGX", "USD"]);
        currencies.Single(c => c.IsoCode == "UGX").MinorUnits.ShouldBe(0);
    }
}
