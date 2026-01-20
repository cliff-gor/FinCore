using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCore.BuildingBlocks.Web;
using FinCore.IntegrationTests.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Api.IntegrationTests;

/// <summary>AC-17 against a test-only idempotent endpoint (no production endpoint uses idempotency in M0).</summary>
[Trait("Category", "Integration")]
public sealed class IdempotencyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly FinCoreApiFactory _factory = new(postgres, configureServices: s => s.AddSingleton<IModule, TestModule>());
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private Task<HttpResponseMessage> PostAsync(string? key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/test/transfers") { Content = JsonContent.Create(body) };
        if (key is not null)
        {
            request.Headers.Add(IdempotencyFilter.HeaderName, key);
        }

        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Replay_with_same_body_returns_stored_response_and_runs_handler_once()
    {
        var key = Guid.NewGuid().ToString();
        var body = new { amount = "10.00", reference = key };

        var first = await PostAsync(key, body);
        var second = await PostAsync(key, body);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        second.Headers.GetValues("Idempotent-Replayed").Single().ShouldBe("true");
        TestModule.Executions.GetValueOrDefault(key).ShouldBe(1);
    }

    [Fact]
    public async Task Same_key_with_different_body_returns_422()
    {
        var key = Guid.NewGuid().ToString();
        (await PostAsync(key, new { amount = "10.00", reference = key })).StatusCode.ShouldBe(HttpStatusCode.Created);

        var conflicting = await PostAsync(key, new { amount = "99.00", reference = key });

        conflicting.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await conflicting.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("code").GetString().ShouldBe("Idempotency.KeyReused");
    }

    [Fact]
    public async Task Missing_key_returns_400()
    {
        var response = await PostAsync(null, new { amount = "10.00", reference = "none" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("code").GetString().ShouldBe("Idempotency.KeyMissing");
    }

    public sealed record TestTransfer(string Amount, string Reference);

    private sealed class TestModule : IModule
    {
        public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Executions = new();

        public string Name => "IdempotencyTest";

        public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration) => services;

        public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost("/test/transfers", (TestTransfer transfer) =>
                {
                    Executions.AddOrUpdate(transfer.Reference, 1, (_, n) => n + 1);
                    return Results.Created($"/test/transfers/{Guid.NewGuid()}", new { id = Guid.NewGuid(), transfer.Amount });
                })
                .RequireIdempotencyKey();
            return app;
        }
    }
}
