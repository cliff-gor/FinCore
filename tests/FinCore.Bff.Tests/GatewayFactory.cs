using System.Collections.Concurrent;
using System.Security.Claims;
using FinCore.Bff.Sessions;
using FinCore.Bff.Tests.Fakes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinCore.Bff.Tests;

/// <summary>
/// The gateway in one mode, proxying to <see cref="FakeApi"/>. Web modes get in-memory sessions and Data Protection
/// keys instead of Postgres, and identity-provider metadata pointing at the fake's <c>/idp/*</c> endpoints.
/// </summary>
internal sealed class GatewayFactory(string mode, string apiAddress, string minimumMobileVersion = "0.1.0") : WebApplicationFactory<Program>
{
    public InMemoryBffSessionRepository Sessions { get; } = new();

    /// <summary>The return URL of every OIDC challenge the gateway issued.</summary>
    public ConcurrentQueue<string?> ChallengeReturnUrls { get; } = new();

    public string CookieName => BffAuthentication.CookieName(Enum.Parse<GatewayMode>(mode));

    /// <summary>
    /// Creates a server-side session (as the OIDC callback would) and returns the matching <c>Cookie</c> header value:
    /// the cookie handler's protected ticket that holds only the session id.
    /// </summary>
    public async Task<string> SignInAsync(string accessToken = "access-1", TimeSpan? accessTokenExpiresIn = null)
    {
        var store = Services.GetRequiredService<BffTicketStore>();
        var ticket = TestTickets.Create(DateTimeOffset.UtcNow + (accessTokenExpiresIn ?? TimeSpan.FromMinutes(5)), accessToken);
        var sessionId = await store.StoreAsync(ticket);

        var cookieOptions = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BffAuthentication.CookieScheme);
        var cookieTicket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("Microsoft.AspNetCore.Authentication.Cookies-SessionId", sessionId)],
                BffAuthentication.CookieScheme)),
            BffAuthentication.CookieScheme);
        return $"{CookieName}={cookieOptions.TicketDataFormat.Protect(cookieTicket)}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Bff:Mode", mode);
        builder.UseSetting("ReverseProxy:Clusters:api:Destinations:d1:Address", apiAddress);
        builder.UseSetting("Mobile:MinimumVersion", minimumMobileVersion);
        builder.UseSetting("Bff:Oidc:ClientSecret", "test-only-secret");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IBffSessionRepository>(Sessions);
            services.Configure<KeyManagementOptions>(o => o.XmlRepository = new InMemoryXmlRepository());
            services.Configure<OpenIdConnectOptions>(BffAuthentication.OidcScheme, o =>
            {
                // A static configuration stops the handler from downloading Keycloak's discovery document.
                o.Configuration = StaticOidcMetadata.Create(apiAddress);
                o.Events.OnRedirectToIdentityProvider = ctx =>
                {
                    ChallengeReturnUrls.Enqueue(ctx.Properties.RedirectUri);
                    return Task.CompletedTask;
                };
            });
        });
    }
}
