using Npgsql;
using Testcontainers.PostgreSql;

namespace FinCore.IntegrationTests.Shared;

/// <summary>
/// One PostgreSQL 17 container per test assembly (Testcontainers). Each test class gets its own database
/// via <see cref="NewDatabaseConnectionString"/>, so tests never see each other's rows.
/// Override the image with <c>FINCORE_TEST_POSTGRES_IMAGE</c> (e.g. a locally cached tag).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Environment.GetEnvironmentVariable("FINCORE_TEST_POSTGRES_IMAGE") ?? "postgres:17-alpine")
        .WithDatabase("fincore")
        .WithUsername("fincore")
        .WithPassword("fincore-test-only")
        .Build();

    public string NewDatabaseConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"fincore_{Guid.NewGuid():N}",
        };
        return builder.ConnectionString;
    }

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}
