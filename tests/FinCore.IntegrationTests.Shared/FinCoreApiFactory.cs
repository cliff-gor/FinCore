using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.IntegrationTests.Shared;

/// <summary>
/// Hosts FinCore.Api in memory against its own Postgres database. Defaults: Development environment
/// (anonymous module access), migrations on start, no dev seed, fast outbox polling.
/// </summary>
public class FinCoreApiFactory(
    PostgresFixture postgres,
    string environment = "Development",
    IReadOnlyDictionary<string, string?>? settings = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = postgres.NewDatabaseConnectionString();

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(environment);

        var defaults = new Dictionary<string, string?>
        {
            ["ConnectionStrings:fincore"] = _connectionString,
            ["Database:MigrateOnStartup"] = "true",
            ["Database:SeedDevData"] = "false",
            ["Seq:ServerUrl"] = string.Empty,
            ["Outbox:PollingInterval"] = "00:00:00.200",
            ["Outbox:BaseRetryDelay"] = "00:00:00.050",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            defaults[key] = value;
        }

        foreach (var (key, value) in defaults)
        {
            builder.UseSetting(key, value);
        }

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
