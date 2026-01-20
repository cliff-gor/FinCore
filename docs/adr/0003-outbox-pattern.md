# ADR-0003: Transactional outbox, in-process bus and inbox

- **Status:** Accepted
- **Context:** State changes in one module must reliably reach others without distributed transactions.
- **Decision:**
  - Aggregates raise domain events. `DomainEventsToOutboxInterceptor` maps them (via per-module `DomainEventMapper`s)
    to integration events from `.Contracts` and writes them to the module's `outbox_messages` **in the same transaction**.
  - `ProcessOutboxJob<TDbContext>` (Quartz, every 2 s) publishes pending messages to `IEventBus`.
  - **Deviation from master spec §7:** the M0 `InMemoryEventBus` dispatches to subscribers synchronously within the
    outbox job instead of through `System.Threading.Channels`. This lets the outbox see subscriber failures and apply
    retries and dead-lettering. Each subscriber runs in its own DI scope.
  - Failures retry with exponential backoff (2 s, 4 s, 8 s, 16 s); after 5 attempts the message is dead-lettered
    (`dead_lettered_on_utc`, error stored, `fincore.outbox.dead_lettered` metric).
  - Subscribers are wrapped in `IdempotentEventHandlerDecorator`, which records `(message_id, handler_name)` in the
    subscribing module's `inbox_messages` in the same transaction as the handler's work → effectively-once handling.
- **Consequences:** At-least-once delivery with idempotent consumers. Swapping to SNS/SQS or RabbitMQ (spec §36)
  replaces only the `IEventBus` implementation. Integration events are JSON-serialized; their CLR type name is stored.
