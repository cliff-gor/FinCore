using System.Net;
using System.Net.Http.Json;
using FinCore.IntegrationTests.Shared;

namespace FinCore.Modules.Organization.IntegrationTests;

/// <summary>Institution endpoints over HTTP against a real Postgres database (spec 001 AC-6 to AC-11).</summary>
[Trait("Category", "Integration")]
public sealed class InstitutionEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly FinCoreApiFactory _factory = new(postgres);
    private HttpClient _client = null!;

    // Creating institutions is PlatformAdmin-only since spec 002.
    public async ValueTask InitializeAsync() => _client = await _factory.CreatePlatformAdminClientAsync();

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_normalized_institution() // AC-7
    {
        var response = await _client.CreateAsync(InstitutionApi.Request("fcb2"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<InstitutionDto>(InstitutionApi.Json, TestContext.Current.CancellationToken))!;
        body.Code.ShouldBe("FCB2");
        body.Status.ShouldBe("Active");
        body.Type.ShouldBe("Bank");
        body.CurrentBusinessDate.ShouldBe(new DateOnly(2026, 10, 14));
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/institutions/{body.Id}");

        var fetched = await _client.GetFromJsonAsync<InstitutionDto>(response.Headers.Location, InstitutionApi.Json, TestContext.Current.CancellationToken);
        fetched!.Code.ShouldBe("FCB2");
    }

    [Fact]
    public async Task Duplicate_code_in_any_case_returns_409() // AC-8 sequential
    {
        (await _client.CreateAsync(InstitutionApi.Request("DUP1"))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await _client.CreateAsync(InstitutionApi.Request("dup1"));

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await duplicate.ProblemAsync();
        problem.GetProperty("code").GetString().ShouldBe("Organization.InstitutionCodeTaken");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty(); // AC-6
    }

    [Fact]
    public async Task Concurrent_creates_with_same_code_yield_one_201_and_409s() // AC-8 race
    {
        // Fire the requests together so some get past the handler's pre-check; the unique index must still catch them.
        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _client.CreateAsync(InstitutionApi.Request("RACE1"))));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(4);
    }

    [Theory] // AC-9
    [InlineData("JPY1", "JPY", "2026-10-14", "Test Bank", "baseCurrency")]
    [InlineData("SAT1", "KES", "2026-10-17", "Test Bank", "initialBusinessDate")]
    [InlineData("AB", "KES", "2026-10-14", "Test Bank", "code")]
    [InlineData("TOOLONG1", "KES", "2026-10-14", "Test Bank", "code")]
    [InlineData("NAME1", "KES", "2026-10-14", "X", "name")]
    public async Task Invalid_requests_return_400_naming_the_field(string code, string currency, string date, string name, string field)
    {
        var response = await _client.CreateAsync(InstitutionApi.Request(code, currency, date, name));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ProblemAsync();
        problem.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue(problem.ToString());
        problem.GetProperty("code").GetString().ShouldBe("Validation.Failed");
    }

    [Fact]
    public async Task Unknown_institution_returns_404_for_get_and_business_date() // AC-11
    {
        var id = Guid.NewGuid();

        foreach (var path in new[] { $"{InstitutionApi.Path}/{id}", $"{InstitutionApi.Path}/{id}/business-date" })
        {
            var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.ProblemAsync()).GetProperty("code").GetString().ShouldBe("Organization.InstitutionNotFound");
        }
    }

    [Fact]
    public async Task Business_date_is_returned_for_known_institution() // AC-11
    {
        var created = (await (await _client.CreateAsync(InstitutionApi.Request("BDATE1", date: "2026-10-16")))
            .Content.ReadFromJsonAsync<InstitutionDto>(InstitutionApi.Json, TestContext.Current.CancellationToken))!;

        var date = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>(
            $"{InstitutionApi.Path}/{created.Id}/business-date", TestContext.Current.CancellationToken);

        date.GetProperty("institutionId").GetGuid().ShouldBe(created.Id);
        date.GetProperty("businessDate").GetString().ShouldBe("2026-10-16");
    }
}
