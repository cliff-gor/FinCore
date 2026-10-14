using Yarp.ReverseProxy.Configuration;

namespace FinCore.Bff.Security;

/// <summary>
/// Development only: serves the web app's Vite dev server through the BFF (<c>Bff:DevSpaUrl</c>), so the browser uses
/// one origin (https://localhost:7200 / :7201) for the SPA, <c>/bff</c>, <c>/api</c> and the OIDC callback.
/// </summary>
/// <remarks>
/// The identity provider returns to <c>https://localhost:720x/signin-oidc</c> and the BFF then redirects to the local
/// return URL, so the app must be reachable on that same origin — the dev equivalent of CloudFront routing
/// <c>/api</c> and <c>/bff</c> to the gateway on AWS, and of the BFF serving the built SPA locally (§30.3). The route
/// has the lowest priority, so <c>/bff</c>, <c>/api</c> and <c>/signin-oidc</c> are always handled first.
/// </remarks>
internal static class DevSpaProxy
{
    public const string RouteId = "dev-spa";

    public static IReverseProxyBuilder AddDevSpaRoute(this IReverseProxyBuilder proxy, string devServerUrl) =>
        proxy.LoadFromMemory(
            [
                new RouteConfig
                {
                    RouteId = RouteId,
                    ClusterId = RouteId,
                    Order = int.MaxValue,
                    Match = new RouteMatch { Path = "/{**catch-all}" },
                },
            ],
            [
                new ClusterConfig
                {
                    ClusterId = RouteId,
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                    {
                        ["vite"] = new() { Address = devServerUrl },
                    },
                },
            ]);
}
