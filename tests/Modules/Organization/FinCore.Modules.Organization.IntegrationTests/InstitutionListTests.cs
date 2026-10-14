using System.Net;
using System.Net.Http.Json;
using FinCore.IntegrationTests.Shared;

namespace FinCore.Modules.Organization.IntegrationTests;

/// <summary>
/// Paging and ordering (spec 001 AC-10). Uses its own database with seeding off, so the total count is exactly the
/// three institutions created here.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InstitutionListTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly FinCoreApiFactory _factory = new(postgres);
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _client = await _factory.CreatePlatformAdminClientAsync();
        foreach (var code in new[] { "CCC", "AAA", "BBB" })
        {
            (await _client.CreateAsync(InstitutionApi.Request(code))).StatusCode.ShouldBe(HttpStatusCode.Created);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Pages_are_ordered_by_code_with_total_count() // AC-10
    {
        var first = await _client.GetFromJsonAsync<PageDto>($"{InstitutionApi.Path}?page=1&pageSize=1", InstitutionApi.Json, TestContext.Current.CancellationToken);
        var second = await _client.GetFromJsonAsync<PageDto>($"{InstitutionApi.Path}?page=2&pageSize=1", InstitutionApi.Json, TestContext.Current.CancellationToken);

        first!.Items.ShouldHaveSingleItem().Code.ShouldBe("AAA");
        first.TotalCount.ShouldBe(3);
        first.Page.ShouldBe(1);
        first.PageSize.ShouldBe(1);
        second!.Items.ShouldHaveSingleItem().Code.ShouldBe("BBB");
    }

    [Theory] // AC-10
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task Out_of_range_paging_returns_400(string query, string field)
    {
        var response = await _client.GetAsync($"{InstitutionApi.Path}?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemAsync()).GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }
}
