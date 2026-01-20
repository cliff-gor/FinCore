using System.Text.RegularExpressions;

namespace FinCore.Bff.Security;

/// <summary>
/// Mobile mode: parses <c>X-App-Version: mobile/1.2.0 (45)</c> and answers <c>426 Upgrade Required</c> below
/// <c>Mobile:MinimumVersion</c> (spec §31.5, §33.7.3). Requests without the header are passed through
/// (spec 001 does not define that case; the app always sends it).
/// </summary>
internal sealed partial class MobileVersionMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string HeaderName = "X-App-Version";
    public const string DefaultMinimumVersion = "0.1.0";

    public async Task InvokeAsync(HttpContext context)
    {
        var header = context.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrEmpty(header))
        {
            var minimum = Version.Parse(configuration["Mobile:MinimumVersion"] ?? DefaultMinimumVersion);
            var match = AppVersionPattern().Match(header);
            if (!match.Success || Version.Parse(match.Groups["version"].Value) < minimum)
            {
                var extensions = new Dictionary<string, object?>
                {
                    ["code"] = "Gateway.UpgradeRequired",
                    ["minimumVersion"] = minimum.ToString(3),
                };
                if (configuration["Mobile:StoreUrl"] is { Length: > 0 } storeUrl)
                {
                    extensions["storeUrl"] = storeUrl;
                }

                await Results.Problem(
                    statusCode: StatusCodes.Status426UpgradeRequired,
                    title: "App update required",
                    detail: $"This version of the app is no longer supported. Please update to {minimum.ToString(3)} or later.",
                    extensions: extensions)
                    .ExecuteAsync(context).ConfigureAwait(false);
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^mobile/(?<version>\d+\.\d+\.\d+)(\s*\((?<build>\d+)\))?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex AppVersionPattern();
}
