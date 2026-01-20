using System.Net.Http.Json;

namespace FinCore.Api.IntegrationTests;

internal static class TestHelpers
{
    public static async Task<Guid> CreateInstitutionAsync(this HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/institutions",
            new { code, name = "Test Bank", type = "Bank", baseCurrency = "KES", initialBusinessDate = "2026-10-14" },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
