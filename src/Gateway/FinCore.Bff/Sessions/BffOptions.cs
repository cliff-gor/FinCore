namespace FinCore.Bff.Sessions;

/// <summary>
/// OpenID Connect client settings for one web gateway mode (<c>Bff:Oidc</c>). BackOffice signs in against the
/// <c>fincore-staff</c> realm as <c>fincore-backoffice</c>; Portal against <c>fincore-customers</c> as
/// <c>fincore-portal</c> (spec 002, Integrations).
/// </summary>
internal sealed class BffOidcOptions
{
    public const string Section = "Bff:Oidc";

    /// <summary>Issuer URL of the realm, e.g. <c>http://localhost:8080/realms/fincore-staff</c>.</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Confidential-client secret. Never stored in appsettings: the dev value comes from launchSettings.json / the
    /// AppHost, any other environment from its secret store.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>Session lifetimes for one web gateway mode (<c>Bff:Session</c>, spec 002 "Session lifetimes").</summary>
internal sealed class BffSessionOptions
{
    public const string Section = "Bff:Session";

    /// <summary>Sliding idle timeout: 30 min for staff (Back Office), 15 min for customers (Portal).</summary>
    public TimeSpan IdleTimeout { get; set; }

    /// <summary>Hard limit regardless of activity: 10 h for staff, 8 h for customers.</summary>
    public TimeSpan AbsoluteLifetime { get; set; }

    /// <summary>
    /// The gateway mode that owns the sessions. Back Office and Portal share one table, so every read checks the mode:
    /// a session created by one app can never be opened by the other.
    /// </summary>
    public string Mode { get; set; } = string.Empty;
}
