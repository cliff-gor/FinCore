using System.Globalization;
using System.Security.Claims;
using FinCore.Bff.Sessions;
using Microsoft.AspNetCore.Authentication;

namespace FinCore.Bff.Tests.Fakes;

/// <summary>Authentication tickets shaped like the ones the OIDC handler produces with <c>SaveTokens</c>.</summary>
internal static class TestTickets
{
    public static AuthenticationTicket Create(
        DateTimeOffset accessTokenExpiresAt,
        string accessToken = "access-1",
        string refreshToken = "refresh-1",
        string subject = "user-1")
    {
        var identity = new ClaimsIdentity(
            [new Claim("sub", subject), new Claim("name", "Test User"), new Claim("email", "test@fincore.local")],
            authenticationType: "oidc",
            nameType: "name",
            roleType: "role");
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken { Name = "id_token", Value = "id-token-1" },
            new AuthenticationToken { Name = "token_type", Value = "Bearer" },
            new AuthenticationToken { Name = BffTicketStore.ExpiresAtToken, Value = accessTokenExpiresAt.ToString("o", CultureInfo.InvariantCulture) },
        ]);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), properties, BffAuthentication.CookieScheme);
    }
}
