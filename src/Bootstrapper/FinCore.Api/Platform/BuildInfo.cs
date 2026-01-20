using System.Reflection;

namespace FinCore.Api.Platform;

/// <summary>Version facts baked in at build time (<c>Version</c>, <c>SourceRevisionId</c>, <c>BuildTimeUtc</c>).</summary>
internal static class BuildInfo
{
    private static readonly Assembly Assembly = typeof(BuildInfo).Assembly;

    private static readonly string Informational =
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static string Product { get; } = Informational.Split('+')[0];

    public static string Commit { get; } = Informational.Contains('+', StringComparison.Ordinal)
        ? Informational.Split('+')[1][..Math.Min(7, Informational.Split('+')[1].Length)]
        : "dev";

    public static string? BuiltAtUtc { get; } = Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "BuildTimeUtc")?.Value;
}
