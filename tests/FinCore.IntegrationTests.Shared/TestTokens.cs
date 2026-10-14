using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinCore.IntegrationTests.Shared;

/// <summary>
/// Signed test tokens (spec 002 AC-23 decision): an in-test issuer per identity pool whose key the test host trusts.
/// Tokens carry the same claims Keycloak issues (sub, email, email_verified, name), so the real authentication and
/// claims-transformation code runs unchanged; only the signature authority differs.
/// </summary>
public static class TestTokens
{
    public const string StaffIssuer = "https://tokens.test/realms/fincore-staff";
    public const string CustomerIssuer = "https://tokens.test/realms/fincore-customers";
    public const string StaffAudience = "fincore-api";
    public const string CustomerAudience = "fincore-api-customers";

    private static readonly RSA Rsa = RSA.Create(2048);

    public static readonly RsaSecurityKey SigningKey = new(Rsa) { KeyId = "fincore-test-key" };

    /// <summary>A staff token. A fresh <paramref name="subject"/> behaves like a first sign-in (linked by email).</summary>
    public static string Staff(string email, string? subject = null, bool emailVerified = true) =>
        Create(StaffIssuer, StaffAudience, subject ?? SubjectFor(email), email, emailVerified);

    public static string Customer(string email, string? subject = null) =>
        Create(CustomerIssuer, CustomerAudience, subject ?? SubjectFor(email), email, emailVerified: true);

    /// <summary>Stable subject per email, like a real IdP account, so repeated sign-ins hit the same linked user.</summary>
    public static string SubjectFor(string email) =>
        new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant()))[..16]).ToString();

    private static string Create(string issuer, string audience, string subject, string email, bool emailVerified)
    {
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", subject),
                new Claim("email", email),
                new Claim("email_verified", emailVerified ? "true" : "false", ClaimValueTypes.Boolean),
                new Claim("name", email.Split('@')[0]),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }
}
