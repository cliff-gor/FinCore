using System.Net.Http.Json;
using FinCore.IntegrationTests.Shared;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.Modules.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Modules.Organization.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ConcurrencyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Concurrent_update_of_an_aggregate_is_rejected_by_xmin()
    {
        await using var factory = new FinCoreApiFactory(postgres);
        using var client = factory.CreateClient();
        var created = (await (await client.CreateAsync(InstitutionApi.Request("XMIN1")))
            .Content.ReadFromJsonAsync<InstitutionDto>(InstitutionApi.Json, TestContext.Current.CancellationToken))!;
        var id = new InstitutionId(created.Id);
        var ct = TestContext.Current.CancellationToken;

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var a = await dbA.Institutions.SingleAsync(i => i.Id == id, ct);
        var b = await dbB.Institutions.SingleAsync(i => i.Id == id, ct);

        dbA.Entry(a).Property(i => i.Name).CurrentValue = "First writer";
        await dbA.SaveChangesAsync(ct);

        dbB.Entry(b).Property(i => i.Name).CurrentValue = "Second writer";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(ct));

        var stored = await dbA.Institutions.AsNoTracking().SingleAsync(i => i.Id == id, ct);
        stored.ModifiedAtUtc.ShouldNotBeNull();
    }
}
