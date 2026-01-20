using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinCore.Modules.Organization.IntegrationTests;

internal sealed record InstitutionDto(
    Guid Id, string Code, string Name, string Type, string BaseCurrency, string? LicenseNumber,
    string Status, DateOnly CurrentBusinessDate, DateTime CreatedAtUtc);

internal sealed record PageDto(List<InstitutionDto> Items, int Page, int PageSize, int TotalCount);

internal static class InstitutionApi
{
    public const string Path = "/api/v1/institutions";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static object Request(string code, string currency = "KES", string date = "2026-10-14", string name = "Test Bank", string type = "Bank") =>
        new { code, name, type, baseCurrency = currency, initialBusinessDate = date };

    public static Task<HttpResponseMessage> CreateAsync(this HttpClient client, object request) =>
        client.PostAsJsonAsync(Path, request, Json, TestContext.Current.CancellationToken);

    public static async Task<JsonElement> ProblemAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
}
