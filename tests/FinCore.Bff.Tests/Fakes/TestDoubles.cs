using System.Xml.Linq;
using FinCore.Bff.Tokens;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FinCore.Bff.Tests.Fakes;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Data Protection key ring in memory, replacing the Postgres key table in gateway tests.</summary>
internal sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly List<XElement> _elements = [];

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_elements)
        {
            return _elements.Select(e => new XElement(e)).ToList();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_elements)
        {
            _elements.Add(new XElement(element));
        }
    }
}

/// <summary>Identity-provider metadata pointing at a fake IdP, instead of a discovery document download.</summary>
internal sealed class StaticOidcMetadata(string idpBaseAddress) : IOidcMetadataProvider
{
    public OpenIdConnectConfiguration Configuration { get; } = Create(idpBaseAddress);

    public Task<OpenIdConnectConfiguration> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Configuration);

    public static OpenIdConnectConfiguration Create(string idpBaseAddress)
    {
        var baseAddress = idpBaseAddress.TrimEnd('/');
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = $"{baseAddress}/idp",
            AuthorizationEndpoint = $"{baseAddress}/idp/auth",
            TokenEndpoint = $"{baseAddress}/idp/token",
            EndSessionEndpoint = $"{baseAddress}/idp/logout",
        };
        configuration.AdditionalData["revocation_endpoint"] = $"{baseAddress}/idp/revoke";
        return configuration;
    }
}

/// <summary>Hands out HttpClients over one handler, for unit tests of classes that take an IHttpClientFactory.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>A token endpoint: counts calls and answers with whatever the test sets up.</summary>
internal sealed class FakeTokenEndpoint : HttpMessageHandler
{
    private int _calls;

    public int Calls => _calls;

    /// <summary>Delay before answering, to keep refreshes overlapping.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(100);

    public Func<int, HttpResponseMessage> Respond { get; set; } = call => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""{"access_token":"access-{{call + 1}}","refresh_token":"refresh-{{call + 1}}","expires_in":300}""",
            System.Text.Encoding.UTF8,
            "application/json"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _calls);
        await Task.Delay(Delay, cancellationToken);
        return Respond(call);
    }
}
