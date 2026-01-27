using FinCore.Bff.Sessions;
using FinCore.Bff.Tests.Fakes;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.BuildingBlocks.Infrastructure.Idempotency;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace FinCore.Bff.Tests;

/// <summary>
/// One PostgreSQL container for <see cref="PostgresSessionStoreTests"/>, migrated with the platform schema the API
/// owns. Override the image with <c>FINCORE_TEST_POSTGRES_IMAGE</c> (e.g. a locally cached tag); Testcontainers itself
/// honours <c>TESTCONTAINERS_RYUK_DISABLED</c>.
/// </summary>
public sealed class PlatformDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Environment.GetEnvironmentVariable("FINCORE_TEST_POSTGRES_IMAGE") ?? "postgres:17-alpine")
        .WithDatabase("fincore")
        .WithUsername("fincore")
        .WithPassword("fincore-test-only")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = _container.GetConnectionString(),
            })
            .Build();
        Services = new ServiceCollection().AddPlatformDatabase(configuration).BuildServiceProvider();

        // The test migrates; the gateway itself never does (the API / Migrator own the schema).
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// <summary>The ticket store on the real <c>platform.bff_sessions</c> table (spec 002 "BFF sessions").</summary>
public sealed class PostgresSessionStoreTests(PlatformDatabaseFixture database) : IClassFixture<PlatformDatabaseFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sessions_round_trip_refresh_expire_and_purge_in_postgres()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider(Start);
        var repository = new PostgresBffSessionRepository(database.Services.GetRequiredService<IServiceScopeFactory>());
        var store = new BffTicketStore(
            repository,
            new EphemeralDataProtectionProvider(),
            Options.Create(new BffSessionOptions { Mode = "BackOffice", IdleTimeout = TimeSpan.FromMinutes(30), AbsoluteLifetime = TimeSpan.FromHours(10) }),
            time,
            NullLogger<BffTicketStore>.Instance);

        var id = await store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), ct);
        var loaded = (await store.RetrieveAsync(id, ct)).ShouldNotBeNull();
        loaded.Properties.GetTokenValue("access_token").ShouldBe("access-1");

        time.Advance(TimeSpan.FromMinutes(4));
        await store.RenewAsync(id, TestTickets.Create(Start.AddMinutes(9), "access-2", "refresh-2"), ct);
        var row = (await repository.FindAsync(id, ct)).ShouldNotBeNull();
        row.AccessTokenExpiresAtUtc.ShouldBe(Start.UtcDateTime.AddMinutes(9));
        row.LastSeenAtUtc.ShouldBe(Start.UtcDateTime.AddMinutes(4));
        (await store.RetrieveAsync(id, ct))!.Properties.GetTokenValue("refresh_token").ShouldBe("refresh-2");

        var idle = await store.StoreAsync(TestTickets.Create(Start.AddMinutes(9), subject: "user-2"), ct);
        time.Advance(TimeSpan.FromMinutes(31));
        var now = time.GetUtcNow().UtcDateTime;
        (await repository.DeleteExpiredAsync("BackOffice", now - TimeSpan.FromMinutes(30), now, ct)).ShouldBe(2);
        (await repository.FindAsync(idle, ct)).ShouldBeNull();
        (await store.RetrieveAsync(id, ct)).ShouldBeNull();
    }
}
