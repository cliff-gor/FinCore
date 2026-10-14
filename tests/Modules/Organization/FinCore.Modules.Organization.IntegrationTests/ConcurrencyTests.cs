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
    // Two contexts load the same row; the second writer must fail instead of silently overwriting the first.
    // No update endpoint exists in M0, so the test drives EF directly to prove the base infrastructure.
    [Fact]
    public async Task Concurrent_update_of_an_aggregate_is_rejected_by_xmin()
    {
        await using var factory = new FinCoreApiFactory(postgres);
        // Every module endpoint needs a signed-in caller since spec 002.
        using var client = await factory.CreatePlatformAdminClientAsync();
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
        // The module unit of work translates EF's exception so handlers can map it to a 409 without referencing EF.
        await Should.ThrowAsync<FinCore.BuildingBlocks.Application.Persistence.ConcurrencyConflictException>(() => dbB.SaveChangesAsync(ct));

        var stored = await dbA.Institutions.AsNoTracking().SingleAsync(i => i.Id == id, ct);
        stored.ModifiedAtUtc.ShouldNotBeNull();
    }
}
