using System.Collections.Concurrent;
using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.IntegrationTests.Shared;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using FinCore.Modules.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Api.IntegrationTests;

/// <summary>
/// The full event pipeline: outbox row in the same transaction, publish, inbox de-duplication and dead-lettering
/// (spec 001 AC-14 to AC-16). Outbox polling and retry delays are shortened by the test factory.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OutboxPipelineTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Created_institution_flows_outbox_to_bus_to_inbox() // AC-14
    {
        await using var factory = new FinCoreApiFactory(postgres);
        using var client = factory.CreateClient();
        var institutionId = await client.CreateInstitutionAsync("OBX1");
        var ct = TestContext.Current.CancellationToken;

        Guid messageId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var message = (await db.OutboxMessages.AsNoTracking().ToListAsync(ct))
                .Single(m => m.Content.Contains(institutionId.ToString(), StringComparison.Ordinal));
            message.Type.ShouldStartWith(typeof(InstitutionCreatedIntegrationEvent).FullName!);
            messageId = message.Id;
        }

        var delivered = await Eventually.HoldsAsync(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var processed = await db.OutboxMessages.AnyAsync(m => m.Id == messageId && m.ProcessedOnUtc != null, ct);
            var inboxed = await db.InboxMessages.AnyAsync(m => m.MessageId == messageId && m.HandlerName.EndsWith("InstitutionCreatedLoggingHandler"), ct);
            return processed && inboxed;
        }, TimeSpan.FromSeconds(10));

        delivered.ShouldBeTrue();
    }

    [Fact]
    public async Task Duplicate_delivery_is_handled_once() // AC-15
    {
        await using var factory = new FinCoreApiFactory(postgres, configureServices: services =>
            services.AddIntegrationEventHandler<InstitutionCreatedIntegrationEvent, CountingHandler, OrganizationDbContext>());
        using var client = factory.CreateClient();
        var bus = factory.Services.GetRequiredService<IEventBus>();
        var evt = new InstitutionCreatedIntegrationEvent(
            Guid.CreateVersion7(), DateTime.UtcNow, Guid.CreateVersion7(), "DUPE", "Dupe", "Bank", "KES", new DateOnly(2026, 10, 14));

        // Simulates the outbox re-publishing after a crash between publish and marking the message processed.
        await bus.PublishAsync(evt, TestContext.Current.CancellationToken);
        await bus.PublishAsync(evt, TestContext.Current.CancellationToken);

        CountingHandler.Calls.GetValueOrDefault(evt.Id).ShouldBe(1);
    }

    [Fact]
    public async Task Always_failing_subscriber_dead_letters_after_five_attempts() // AC-16
    {
        await using var factory = new FinCoreApiFactory(postgres, configureServices: services =>
            services.AddIntegrationEventHandler<InstitutionCreatedIntegrationEvent, FailingHandler, OrganizationDbContext>());
        using var client = factory.CreateClient();
        var institutionId = await client.CreateInstitutionAsync("DEAD1");
        var ct = TestContext.Current.CancellationToken;

        var deadLettered = await Eventually.HoldsAsync(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            return (await db.OutboxMessages.AsNoTracking().ToListAsync(ct))
                .Any(m => m.Content.Contains(institutionId.ToString(), StringComparison.Ordinal) && m.DeadLetteredOnUtc != null);
        }, TimeSpan.FromSeconds(30));

        deadLettered.ShouldBeTrue();
        await using var check = factory.Services.CreateAsyncScope();
        var message = (await check.ServiceProvider.GetRequiredService<OrganizationDbContext>().OutboxMessages.AsNoTracking().ToListAsync(ct))
            .Single(m => m.Content.Contains(institutionId.ToString(), StringComparison.Ordinal));
        message.Attempts.ShouldBe(5);
        message.ProcessedOnUtc.ShouldBeNull();
        message.Error!.ShouldContain(FailingHandler.Message);
    }

    internal sealed class CountingHandler : IIntegrationEventHandler<InstitutionCreatedIntegrationEvent>
    {
        // Static because each delivery resolves a fresh handler instance; keyed by event id so tests don't interfere.
        public static readonly ConcurrentDictionary<Guid, int> Calls = new();

        public Task Handle(InstitutionCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Calls.AddOrUpdate(integrationEvent.Id, 1, (_, n) => n + 1);
            return Task.CompletedTask;
        }
    }

    internal sealed class FailingHandler : IIntegrationEventHandler<InstitutionCreatedIntegrationEvent>
    {
        public const string Message = "Simulated subscriber failure";

        public Task Handle(InstitutionCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);
    }
}
