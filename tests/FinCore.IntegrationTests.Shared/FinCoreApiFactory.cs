using System.Net.Http.Headers;
using FinCore.BuildingBlocks.Web.Authorization;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FinCore.IntegrationTests.Shared;

/// <summary>
/// Hosts FinCore.Api in memory against its own Postgres database, trusting <see cref="TestTokens"/> instead of
/// Keycloak. Defaults: Development environment, migrations on start, no dev seed, fast outbox polling.
/// </summary>
public class FinCoreApiFactory(
    PostgresFixture postgres,
    string environment = "Development",
    IReadOnlyDictionary<string, string?>? settings = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    public const string PlatformAdminEmail = "platform.admin@tests.local";

    private readonly string _connectionString = postgres.NewDatabaseConnectionString();

    public string ConnectionString => _connectionString;

    /// <summary>An HTTP client signed in as the given staff email.</summary>
    public HttpClient CreateStaffClient(string email, bool emailVerified = true) =>
        WithBearer(TestTokens.Staff(email, emailVerified: emailVerified));

    public HttpClient CreateCustomerClient(string email) => WithBearer(TestTokens.Customer(email));

    /// <summary>
    /// Signs in as a PlatformAdmin, creating that staff record first. Platform admins are provisioned by operations,
    /// not through the API, so tests create one directly in the Identity schema.
    /// </summary>
    public async Task<HttpClient> CreatePlatformAdminClientAsync()
    {
        _ = Server; // ensure the host (and its migrations) has started
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (!await db.StaffUsers.AnyAsync(s => s.Email == PlatformAdminEmail).ConfigureAwait(false))
        {
            db.StaffUsers.Add(StaffUser.RegisterPlatformAdmin(PlatformAdminEmail, "Platform Admin", DateTime.UtcNow).Value);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        return CreateStaffClient(PlatformAdminEmail);
    }

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
            // The issuers drive scheme selection and the staff/customer split in the claims transformation.
            ["Auth:Staff:Authority"] = string.Empty,
            ["Auth:Staff:Issuer"] = TestTokens.StaffIssuer,
            ["Auth:Customers:Authority"] = string.Empty,
            ["Auth:Customers:Issuer"] = TestTokens.CustomerIssuer,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            defaults[key] = value;
        }

        foreach (var (key, value) in defaults)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            // Trust the in-test signing key instead of downloading Keycloak's keys.
            Trust(services, FinCoreAuthentication.StaffScheme, TestTokens.StaffIssuer, TestTokens.StaffAudience);
            Trust(services, FinCoreAuthentication.CustomerScheme, TestTokens.CustomerIssuer, TestTokens.CustomerAudience);
            configureServices?.Invoke(services);
        });
    }

    private static void Trust(IServiceCollection services, string scheme, string issuer, string audience) =>
        services.PostConfigure<JwtBearerOptions>(scheme, o =>
        {
            o.Authority = null;
            o.MetadataAddress = null!;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = TestTokens.SigningKey,
                ValidateLifetime = true,
            };
        });

    private HttpClient WithBearer(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
