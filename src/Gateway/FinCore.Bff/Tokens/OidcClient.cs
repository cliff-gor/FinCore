using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinCore.Bff.Sessions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FinCore.Bff.Tokens;

/// <summary>Where the identity provider's endpoints are; normally its discovery document.</summary>
internal interface IOidcMetadataProvider
{
    Task<OpenIdConnectConfiguration> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reuses the discovery document the OIDC handler already fetches and caches, so the BFF never downloads it twice
/// and tests can supply a static configuration in one place.
/// </summary>
internal sealed class OpenIdConnectMetadataProvider(IOptionsMonitor<OpenIdConnectOptions> options) : IOidcMetadataProvider
{
    public Task<OpenIdConnectConfiguration> GetAsync(CancellationToken cancellationToken)
    {
        var manager = options.Get(BffAuthentication.OidcScheme).ConfigurationManager
            ?? throw new InvalidOperationException("The OpenID Connect handler has no configuration manager.");
        return manager.GetConfigurationAsync(cancellationToken);
    }
}

/// <summary>A token set from the identity provider. <see cref="ExpiresAtUtc"/> is when the access token expires.</summary>
internal sealed record TokenSet(string AccessToken, string? RefreshToken, string? IdToken, DateTimeOffset ExpiresAtUtc);

/// <summary>Outcome of a refresh: new tokens, a definite "no" from the provider, or the provider not answering.</summary>
internal enum RefreshStatus
{
    Refreshed,
    Rejected,
    Unavailable,
}

internal sealed record RefreshResult(RefreshStatus Status, TokenSet? Tokens = null);

/// <summary>
/// The back-channel calls the BFF makes as a confidential client: refreshing the access token, revoking the refresh
/// token at sign-out, and building the RP-initiated logout URL (spec 002 "BFF sessions").
/// </summary>
internal sealed partial class OidcClient(
    IHttpClientFactory httpClientFactory,
    IOidcMetadataProvider metadata,
    IOptions<BffOidcOptions> options,
    TimeProvider timeProvider,
    ILogger<OidcClient> logger)
{
    public const string HttpClientName = "fincore-oidc";

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = await metadata.GetAsync(cancellationToken).ConfigureAwait(false);
            using var content = ClientForm(
                ("grant_type", "refresh_token"),
                ("refresh_token", refreshToken));
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsync(configuration.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);

            // 4xx (invalid_grant: expired, revoked or already-rotated refresh token) is final; 5xx may be a blip.
            if ((int)response.StatusCode is >= 400 and < 500)
            {
                LogRefreshRejected(logger, (int)response.StatusCode);
                return new RefreshResult(RefreshStatus.Rejected);
            }

            if (!response.IsSuccessStatusCode)
            {
                LogProviderUnavailable(logger, (int)response.StatusCode, null);
                return new RefreshResult(RefreshStatus.Unavailable);
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);
            if (body?.AccessToken is not { Length: > 0 } accessToken)
            {
                return new RefreshResult(RefreshStatus.Rejected);
            }

            var expiresAt = timeProvider.GetUtcNow().AddSeconds(body.ExpiresIn ?? 0);
            return new RefreshResult(RefreshStatus.Refreshed, new TokenSet(accessToken, body.RefreshToken, body.IdToken, expiresAt));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            LogProviderUnavailable(logger, 0, ex);
            return new RefreshResult(RefreshStatus.Unavailable);
        }
    }

    /// <summary>
    /// Revokes the refresh token (RFC 7009) so a copy that leaked cannot be used after sign-out. Best effort: sign-out
    /// still completes when the provider is down, because the server-side session is deleted either way.
    /// </summary>
    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = await metadata.GetAsync(cancellationToken).ConfigureAwait(false);
            if (!configuration.AdditionalData.TryGetValue("revocation_endpoint", out var endpoint) || endpoint is not string revocationEndpoint)
            {
                LogNoRevocationEndpoint(logger);
                return;
            }

            using var content = ClientForm(("token", refreshToken), ("token_type_hint", "refresh_token"));
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsync(new Uri(revocationEndpoint), content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRevocationFailed(logger, (int)response.StatusCode, null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogRevocationFailed(logger, 0, ex);
        }
    }

    /// <summary>
    /// The provider's end-session URL with <c>id_token_hint</c> and <c>post_logout_redirect_uri</c>, which the SPA
    /// navigates to so the identity provider's own SSO session ends too. Null when the provider has no such endpoint.
    /// </summary>
    public async Task<string?> EndSessionUrlAsync(string? idToken, string postLogoutRedirectUri, CancellationToken cancellationToken)
    {
        var configuration = await metadata.GetAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(configuration.EndSessionEndpoint))
        {
            return null;
        }

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = options.Value.ClientId,
            ["post_logout_redirect_uri"] = postLogoutRedirectUri,
        };
        // Without the hint Keycloak asks the user to confirm the logout; with it the redirect is immediate.
        if (!string.IsNullOrEmpty(idToken))
        {
            query["id_token_hint"] = idToken;
        }

        return QueryHelpers.AddQueryString(configuration.EndSessionEndpoint, query);
    }

    // client_secret_post: the confidential client authenticates in the form body (Keycloak accepts it for
    // client-secret clients, and it avoids Basic-auth encoding pitfalls with special characters in secrets).
    private FormUrlEncodedContent ClientForm(params (string Name, string Value)[] fields)
    {
        var values = fields.Select(f => KeyValuePair.Create(f.Name, f.Value)).ToList();
        values.Add(KeyValuePair.Create("client_id", options.Value.ClientId));
        values.Add(KeyValuePair.Create("client_secret", options.Value.ClientSecret));
        return new FormUrlEncodedContent(values);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The identity provider rejected a refresh token (HTTP {StatusCode}); the session ends.")]
    private static partial void LogRefreshRejected(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The identity provider did not answer a token refresh (HTTP {StatusCode}).")]
    private static partial void LogProviderUnavailable(ILogger logger, int statusCode, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The identity provider publishes no revocation endpoint; the refresh token was not revoked.")]
    private static partial void LogNoRevocationEndpoint(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Revoking the refresh token failed (HTTP {StatusCode}); the session was still ended.")]
    private static partial void LogRevocationFailed(ILogger logger, int statusCode, Exception? exception);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);
}
