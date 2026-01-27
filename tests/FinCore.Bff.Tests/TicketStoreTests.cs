using FinCore.Bff.Sessions;
using FinCore.Bff.Tests.Fakes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinCore.Bff.Tests;

/// <summary>
/// Server-side session rules of <see cref="BffTicketStore"/> (spec 002 "BFF sessions", "Session lifetimes", AC-3),
/// with an in-memory repository and a hand-moved clock.
/// </summary>
public sealed class TicketStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryBffSessionRepository _repository = new();
    private readonly ManualTimeProvider _time = new(Start);
    private readonly BffTicketStore _store;

    public TicketStoreTests() => _store = CreateStore("BackOffice", TimeSpan.FromMinutes(30), TimeSpan.FromHours(10));

    [Fact]
    public async Task Round_trip_keeps_principal_and_tokens_and_stores_them_encrypted()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);

        var row = _repository[id].ShouldNotBeNull();
        row.Subject.ShouldBe("user-1");
        row.Mode.ShouldBe("BackOffice");
        row.AbsoluteExpiresAtUtc.ShouldBe(Start.UtcDateTime.AddHours(10));
        row.AccessTokenExpiresAtUtc.ShouldBe(Start.UtcDateTime.AddMinutes(5));
        row.ProtectedTokens.ShouldNotContain("access-1");
        id.Length.ShouldBeGreaterThanOrEqualTo(43); // 256 random bits

        var ticket = (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        ticket.Principal.FindFirst("sub")!.Value.ShouldBe("user-1");
        ticket.Properties.GetTokenValue("access_token").ShouldBe("access-1");
        ticket.Properties.GetTokenValue("refresh_token").ShouldBe("refresh-1");
        ticket.Properties.Items[BffTicketStore.SessionIdItem].ShouldBe(id);
    }

    [Fact]
    public async Task Idle_session_expires_and_is_deleted()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromMinutes(30));

        (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
        _repository[id].ShouldBeNull();
    }

    [Fact]
    public async Task Activity_slides_the_idle_timeout()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);

        for (var i = 0; i < 4; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(20));
            (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Absolute_lifetime_ends_an_active_session()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);

        // Active every 20 minutes, so never idle: only the 10 h limit can end it.
        for (var elapsed = TimeSpan.Zero; elapsed < TimeSpan.FromHours(10) - TimeSpan.FromMinutes(20); elapsed += TimeSpan.FromMinutes(20))
        {
            _time.Advance(TimeSpan.FromMinutes(20));
            (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        }

        _time.Advance(TimeSpan.FromMinutes(20));
        (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
        _repository[id].ShouldBeNull();
    }

    [Fact]
    public async Task Customer_sessions_use_the_shorter_portal_lifetimes()
    {
        var portal = CreateStore("Portal", TimeSpan.FromMinutes(15), TimeSpan.FromHours(8));
        var id = await portal.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);
        _repository[id]!.AbsoluteExpiresAtUtc.ShouldBe(Start.UtcDateTime.AddHours(8));

        _time.Advance(TimeSpan.FromMinutes(15));

        (await portal.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Revoked_session_is_rejected_and_deleted()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);
        _repository.Revoke(id, Start.UtcDateTime);

        (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
        _repository[id].ShouldBeNull();
    }

    [Fact]
    public async Task Removed_session_is_gone()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);

        await _store.RemoveAsync(id, TestContext.Current.CancellationToken);

        (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_never_opens_in_the_other_web_app()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);
        var portal = CreateStore("Portal", TimeSpan.FromMinutes(15), TimeSpan.FromHours(8));

        (await portal.RetrieveAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
        _repository[id].ShouldNotBeNull(); // still valid in the Back Office
    }

    [Fact]
    public async Task Renew_never_replaces_newer_tokens_with_older_ones()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(1)), TestContext.Current.CancellationToken);
        var stale = (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken))!;

        // Another request refreshed meanwhile (rotation: refresh-1 is now spent).
        await _store.RenewAsync(id, TestTickets.Create(Start.AddMinutes(6), "access-2", "refresh-2"), TestContext.Current.CancellationToken);
        // The first request's sliding renewal writes back the ticket it loaded.
        await _store.RenewAsync(id, stale, TestContext.Current.CancellationToken);

        var current = (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken))!;
        current.Properties.GetTokenValue("refresh_token").ShouldBe("refresh-2");
        current.Properties.GetTokenValue("access_token").ShouldBe("access-2");
    }

    [Fact]
    public async Task Renew_does_not_resurrect_a_deleted_session()
    {
        var id = await _store.StoreAsync(TestTickets.Create(Start.AddMinutes(5)), TestContext.Current.CancellationToken);
        var ticket = (await _store.RetrieveAsync(id, TestContext.Current.CancellationToken))!;
        await _store.RemoveAsync(id, TestContext.Current.CancellationToken);

        await _store.RenewAsync(id, ticket, TestContext.Current.CancellationToken);

        _repository[id].ShouldBeNull();
    }

    private BffTicketStore CreateStore(string mode, TimeSpan idle, TimeSpan absolute) => new(
        _repository,
        new EphemeralDataProtectionProvider(),
        Options.Create(new BffSessionOptions { Mode = mode, IdleTimeout = idle, AbsoluteLifetime = absolute }),
        _time,
        NullLogger<BffTicketStore>.Instance);
}
