using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinCore.IntegrationTests.Shared;

namespace FinCore.Modules.Identity.IntegrationTests;

/// <summary>Thin HTTP helpers so tests read like the API contract in spec 002.</summary>
internal static class Api
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json, Ct);

    public static async Task<string?> CodeAsync(this HttpResponseMessage response) =>
        (await response.ReadAsync()).TryGetProperty("code", out var code) ? code.GetString() : null;

    public static Task<HttpResponseMessage> Post(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, Json, Ct);

    public static Task<HttpResponseMessage> Put(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json, Ct);

    public static Task<HttpResponseMessage> Get(this HttpClient client, string url) =>
        client.GetAsync(new Uri(url, UriKind.Relative), Ct);

    public static async Task<JsonElement> OkAsync(this Task<HttpResponseMessage> call, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await call;
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(expected, body);
        return string.IsNullOrEmpty(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public static Guid Id(this JsonElement element) => element.GetProperty("id").GetGuid();
}

/// <summary>
/// One institution set up the way spec 002 prescribes: PlatformAdmin creates it and its branches, bootstraps the first
/// SystemAdmin (A), and a second SystemAdmin (B) is added through a normal role grant approved by the PlatformAdmin.
/// </summary>
internal sealed class Tenant
{
    public required Guid InstitutionId { get; init; }

    public required string Code { get; init; }

    public required Guid HeadOffice { get; init; }

    public required Guid CityBranch { get; init; }

    public required HttpClient Platform { get; init; }

    public required HttpClient AdminA { get; init; }

    public required HttpClient AdminB { get; init; }

    public required Guid AdminAId { get; init; }

    public required Guid AdminBId { get; init; }

    public static async Task<Tenant> CreateAsync(FinCoreApiFactory factory, string code)
    {
        var platform = await factory.CreatePlatformAdminClientAsync();
        var institution = await platform.Post("/api/v1/institutions", new
        {
            code, name = $"{code} Bank", type = "Bank", baseCurrency = "KES", initialBusinessDate = "2026-10-14",
        }).OkAsync(HttpStatusCode.Created);
        var id = institution.Id();

        var head = await platform.Post($"/api/v1/institutions/{id}/branches", new { code = "001", name = "Head Office" }).OkAsync(HttpStatusCode.Created);
        var city = await platform.Post($"/api/v1/institutions/{id}/branches", new { code = "002", name = "City Branch" }).OkAsync(HttpStatusCode.Created);

        var emailA = $"admin.a@{code.ToLowerInvariant()}.test";
        var emailB = $"admin.b@{code.ToLowerInvariant()}.test";
        var adminA = await platform.Post("/api/v1/staff", new
        {
            email = emailA, name = "Admin A", homeBranchId = head.Id(), institutionId = id, bootstrapSystemAdmin = true,
        }).OkAsync(HttpStatusCode.Created);

        var clientA = factory.CreateStaffClient(emailA);
        var adminB = await clientA.Post("/api/v1/staff", new { email = emailB, name = "Admin B", homeBranchId = head.Id() })
            .OkAsync(HttpStatusCode.Created);
        var request = await clientA.Post($"/api/v1/staff/{adminB.Id()}/roles", new { role = "SystemAdmin" }).OkAsync(HttpStatusCode.Accepted);
        await platform.Post($"/api/v1/approvals/{request.Id()}/approve").OkAsync();

        return new Tenant
        {
            InstitutionId = id,
            Code = code,
            HeadOffice = head.Id(),
            CityBranch = city.Id(),
            Platform = platform,
            AdminA = clientA,
            AdminB = factory.CreateStaffClient(emailB),
            AdminAId = adminA.Id(),
            AdminBId = adminB.Id(),
        };
    }

    /// <summary>Registers a staff user (no roles) and returns its id and email.</summary>
    public async Task<(Guid Id, string Email)> RegisterAsync(string name, Guid? branch = null)
    {
        var email = $"{name.Replace(' ', '.').ToLowerInvariant()}@{Code.ToLowerInvariant()}.test";
        var staff = await AdminA.Post("/api/v1/staff", new { email, name, homeBranchId = branch ?? HeadOffice })
            .OkAsync(HttpStatusCode.Created);
        return (staff.Id(), email);
    }

    /// <summary>A requests a role for the user, B approves it.</summary>
    public async Task GrantAsync(Guid staffId, string role)
    {
        var request = await AdminA.Post($"/api/v1/staff/{staffId}/roles", new { role }).OkAsync(HttpStatusCode.Accepted);
        await AdminB.Post($"/api/v1/approvals/{request.Id()}/approve").OkAsync();
    }
}
