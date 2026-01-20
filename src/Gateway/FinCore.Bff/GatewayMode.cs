namespace FinCore.Bff;

/// <summary>
/// One gateway project, three modes (spec §30.3, §31.4): cookie-session BFFs for the two web apps and a
/// token-passthrough Mobile Gateway. Selected by <c>Bff:Mode</c>.
/// </summary>
public enum GatewayMode
{
    BackOffice,
    Portal,
    Mobile,
}

public static class GatewayModeConfiguration
{
    public const string Key = "Bff:Mode";

    public static GatewayMode Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[Key];
        return Enum.TryParse<GatewayMode>(raw, ignoreCase: true, out var mode)
            ? mode
            : throw new InvalidOperationException($"'{Key}' must be one of BackOffice, Portal or Mobile (was '{raw}').");
    }

    public static bool IsWeb(this GatewayMode mode) => mode is GatewayMode.BackOffice or GatewayMode.Portal;
}
