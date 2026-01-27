using System.Threading.RateLimiting;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.BuildingBlocks.Infrastructure.Idempotency;
using FinCore.Bff.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace FinCore.Bff.Sessions;

/// <summary>
/// Cookie + OpenID Connect sign-in for the two web modes (master spec §30.3, spec 002 "BFF sessions"): Authorization
/// Code + PKCE against the mode's realm, tokens kept server-side in <see cref="BffTicketStore"/>, and a cookie that
/// holds only the session id.
/// </summary>
internal static class BffAuthentication
{
    public const string CookieScheme = "fincore-session";
    public const string OidcScheme = "fincore-oidc";

    /// <summary>Rate-limiter policy for <c>/bff/login</c> (spec 002 NFR: 10 requests per minute per IP).</summary>
    public const string LoginRateLimitPolicy = "bff-login";

    /// <summary>Session cookie name per mode, e.g. <c>__Host-fincore-backoffice</c>.</summary>
    public static string CookieName(GatewayMode mode) => $"__Host-fincore-{mode.ToString().ToLowerInvariant()}";

    public static WebApplicationBuilder AddBffSessions(this WebApplicationBuilder builder, GatewayMode mode)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        var oidcSection = configuration.GetSection(BffOidcOptions.Section);
        var oidcSettings = oidcSection.Get<BffOidcOptions>() ?? new BffOidcOptions();
        // Fail at start-up, not at the first sign-in, when the deployment forgot a setting.
        if (string.IsNullOrWhiteSpace(oidcSettings.Authority)
            || string.IsNullOrWhiteSpace(oidcSettings.ClientId)
            || string.IsNullOrWhiteSpace(oidcSettings.ClientSecret))
        {
            throw new InvalidOperationException(
                $"'{BffOidcOptions.Section}:Authority', ':ClientId' and ':ClientSecret' must be set for the {mode} gateway.");
        }

        services.Configure<BffOidcOptions>(oidcSection);
        services.AddOptions<BffSessionOptions>()
            .Bind(configuration.GetSection(BffSessionOptions.Section))
            .Configure(o => o.Mode = mode.ToString())
            .Validate(o => o.IdleTimeout > TimeSpan.Zero && o.AbsoluteLifetime >= o.IdleTimeout, "Bff:Session lifetimes are invalid.")
            .ValidateOnStart();

        // Sessions and Data Protection keys live in the platform schema. The API (Migrator in AWS) owns its migrations;
        // the gateway only reads and writes rows.
        services.AddPlatformDatabase(configuration);
        // Keys in Postgres let every gateway instance decrypt every session and keep sessions valid across restarts
        // (§33.5). The application name isolates the BFF's protected payloads from the API's.
        services.AddDataProtection()
            .SetApplicationName("FinCore.Bff")
            .PersistKeysToDbContext<PlatformDbContext>();

        services.AddSingleton<IBffSessionRepository, PostgresBffSessionRepository>();
        services.AddSingleton<BffTicketStore>();
        services.AddHostedService<ExpiredSessionPurger>();
        services.AddSingleton<IOidcMetadataProvider, OpenIdConnectMetadataProvider>();
        services.AddSingleton<OidcClient>();
        services.AddSingleton<SessionTokenService>();
        services.AddHttpClient(OidcClient.HttpClientName);
        services.AddHttpClient(BffEndpoints.ApiClientName, (sp, client) =>
            client.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["ReverseProxy:Clusters:api:Destinations:d1:Address"]
                ?? throw new InvalidOperationException("The API address (ReverseProxy:Clusters:api) is not configured.")));

        var isDevelopment = builder.Environment.IsDevelopment();
        services
            .AddAuthentication(o =>
            {
                o.DefaultScheme = CookieScheme;
                o.DefaultChallengeScheme = OidcScheme;
            })
            .AddCookie(CookieScheme, o =>
            {
                // __Host- makes the browser enforce Secure, Path=/ and no Domain, so no sibling subdomain can set or
                // overwrite the session cookie. HttpOnly keeps it away from JavaScript; SameSite=Strict means
                // cross-site requests never carry it (spec 002 NFR, AC-3).
                o.Cookie.Name = CookieName(mode);
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.Path = "/";
                // Matches the idle timeout; the authoritative idle and absolute checks are server-side in the store.
                o.ExpireTimeSpan = configuration.GetValue<TimeSpan?>($"{BffSessionOptions.Section}:IdleTimeout") ?? TimeSpan.FromMinutes(15);
                o.SlidingExpiration = true;
                // The SPA calls /bff and /api with fetch: answer 401/403 instead of redirecting to a login page.
                o.Events.OnRedirectToLogin = ctx => SetStatus(ctx.Response, StatusCodes.Status401Unauthorized);
                o.Events.OnRedirectToAccessDenied = ctx => SetStatus(ctx.Response, StatusCodes.Status403Forbidden);
            })
            .AddOpenIdConnect(OidcScheme, o =>
            {
                o.Authority = oidcSettings.Authority;
                o.ClientId = oidcSettings.ClientId;
                o.ClientSecret = oidcSettings.ClientSecret;
                o.SignInScheme = CookieScheme;
                o.ResponseType = "code";
                o.UsePkce = true;
                o.SaveTokens = true;
                o.MapInboundClaims = false;
                o.GetClaimsFromUserInfoEndpoint = false;
                o.TokenValidationParameters.NameClaimType = "name";
                o.Scope.Clear();
                // No offline_access: the refresh token stays bound to the IdP's SSO session and its lifetimes.
                o.Scope.Add("openid");
                o.Scope.Add("profile");
                o.Scope.Add("email");
                o.CallbackPath = "/signin-oidc";
                // Local Keycloak is plain HTTP; every other environment must fetch metadata over HTTPS.
                o.RequireHttpsMetadata = !isDevelopment;

                // The correlation and nonce cookies deliberately keep the framework default SameSite=None (+Secure,
                // HttpOnly, 15 min): the identity provider returns with a cross-site form POST to /signin-oidc, which
                // a Strict or Lax cookie would not accompany, and sign-in would fail with "correlation failed". They
                // only protect the sign-in round trip; the session cookie itself stays Strict.
                o.Events.OnRemoteFailure = ctx =>
                {
                    // Cancelled or failed sign-in (e.g. the user pressed back at Keycloak): return to the app signed
                    // out instead of showing an exception page.
                    ctx.Response.Redirect("/");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                };
            });

        services.AddOptions<CookieAuthenticationOptions>(CookieScheme)
            .Configure<BffTicketStore>((o, store) => o.SessionStore = store);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Fixed window per client IP. Behind a load balancer RemoteIpAddress is the balancer's address, so the
            // forwarded-headers middleware must be configured with the trusted proxies before this runs there.
            // TODO(M9): trust the ALB/CloudFront forwarded headers when the gateway is deployed behind them.
            o.AddPolicy(LoginRateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return builder;
    }

    private static Task SetStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
