using System.Net;
using FinCore.Bff.Sessions;
using FinCore.Bff.Tests.Fakes;
using FinCore.Bff.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinCore.Bff.Tests;

/// <summary>
/// Silent access-token refresh in <see cref="SessionTokenService"/> against a fake token endpoint (spec 002: 5-minute
/// access tokens refreshed by the BFF; on refresh failure the session ends).
/// </summary>
public sealed class TokenRefreshTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryBffSessionRepository _repository = new();
    private readonly ManualTimeProvider _time = new(Start);
    private readonly FakeTokenEndpoint _tokenEndpoint = new();
    private readonly BffTicketStore _store;
    private readonly SessionTokenService _tokens;

    public TokenRefreshTests()
    {
        _store = new BffTicketStore(
            _repository,
            new EphemeralDataProtectionProvider(),
            Options.Create(new BffSessionOptions { Mode = "BackOffice", IdleTimeout = TimeSpan.FromMinutes(30), AbsoluteLifetime = TimeSpan.FromHours(10) }),
            _time,
            NullLogger<BffTicketStore>.Instance);
        var oidc = new OidcClient(
            new StubHttpClientFactory(_tokenEndpoint),
            new StaticOidcMetadata("https://idp.test"),
            Options.Create(new BffOidcOptions { Authority = "https://idp.test", ClientId = "fincore-backoffice", ClientSecret = "test-only" }),
            _time,
            NullLogger<OidcClient>.Instance);
        _tokens = new SessionTokenService(_store, oidc, _time);
    }

    public void Dispose() => _tokenEndpoint.Dispose();

    [Fact]
    public async Task Token_valid_for_more_than_a_minute_is_used_as_is()
    {
        var properties = await SignInAsync(accessTokenExpiresIn: TimeSpan.FromMinutes(4));

        var result = await _tokens.EnsureFreshAsync(properties, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(AccessTokenStatus.Valid);
        result.AccessToken.ShouldBe("access-1");
        _tokenEndpoint.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_requests_share_a_single_refresh()
    {
        var id = (await SignInAsync(accessTokenExpiresIn: TimeSpan.FromSeconds(30))).Items[BffTicketStore.SessionIdItem]!;
        // Ten requests of one page load, each with the ticket it loaded from the store.
        var loaded = new List<AuthenticationProperties>();
        for (var i = 0; i < 10; i++)
        {
            loaded.Add((await _store.RetrieveAsync(id, TestContext.Current.CancellationToken))!.Properties);
        }

        var results = await Task.WhenAll(loaded.Select(p => _tokens.EnsureFreshAsync(p, TestContext.Current.CancellationToken)));

        _tokenEndpoint.Calls.ShouldBe(1);
        results.ShouldAllBe(r => r.Status == AccessTokenStatus.Valid && r.AccessToken == "access-2");
        loaded.ShouldAllBe(p => p.GetTokenValue("refresh_token") == "refresh-2");

        var stored = (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken))!.Properties;
        stored.GetTokenValue("access_token").ShouldBe("access-2");
        stored.GetTokenValue("refresh_token").ShouldBe("refresh-2");
        stored.GetTokenValue("id_token").ShouldBe("id-token-1"); // kept when the provider sends none
        BffTicketStore.AccessTokenExpiresAt(stored).ShouldBe(Start.UtcDateTime.AddMinutes(5));
    }

    [Fact]
    public async Task A_late_request_with_a_stale_ticket_reuses_the_stored_refresh()
    {
        var stale = await SignInAsync(accessTokenExpiresIn: TimeSpan.FromSeconds(30));
        var id = stale.Items[BffTicketStore.SessionIdItem]!;
        var first = (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken))!.Properties;
        (await _tokens.EnsureFreshAsync(first, TestContext.Current.CancellationToken)).AccessToken.ShouldBe("access-2");

        // Loaded before the refresh, finishing after it: must not spend the rotated-out refresh-1.
        var late = await _tokens.EnsureFreshAsync(stale, TestContext.Current.CancellationToken);

        late.AccessToken.ShouldBe("access-2");
        _tokenEndpoint.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Rejected_refresh_ends_the_session()
    {
        _tokenEndpoint.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":"invalid_grant"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        var properties = await SignInAsync(accessTokenExpiresIn: TimeSpan.FromSeconds(30));

        var result = await _tokens.EnsureFreshAsync(properties, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(AccessTokenStatus.SessionEnded);
        _repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unreachable_provider_keeps_the_session_for_a_retry()
    {
        _tokenEndpoint.Respond = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var properties = await SignInAsync(accessTokenExpiresIn: TimeSpan.FromSeconds(30));

        var result = await _tokens.EnsureFreshAsync(properties, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(AccessTokenStatus.ProviderUnavailable);
        _repository.All.Count.ShouldBe(1);
    }

    private async Task<AuthenticationProperties> SignInAsync(TimeSpan accessTokenExpiresIn)
    {
        var ticket = TestTickets.Create(Start + accessTokenExpiresIn);
        await _store.StoreAsync(ticket, TestContext.Current.CancellationToken);
        return ticket.Properties;
    }
}
