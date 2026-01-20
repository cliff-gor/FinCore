using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinCore.Bff.Tests;

internal sealed class GatewayFactory(string mode, string apiAddress, string minimumMobileVersion = "0.1.0") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Bff:Mode", mode);
        builder.UseSetting("ReverseProxy:Clusters:api:Destinations:d1:Address", apiAddress);
        builder.UseSetting("Mobile:MinimumVersion", minimumMobileVersion);
    }
}
