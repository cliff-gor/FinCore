using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace FinCore.BuildingBlocks.Infrastructure.Outbox;

/// <summary>Custom meters <c>fincore.outbox.pending</c> and <c>fincore.outbox.dead_lettered</c> (spec §27).</summary>
public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "FinCore.Outbox";

    private readonly Meter _meter;
    private readonly Counter<long> _deadLettered;
    private readonly ConcurrentDictionary<string, long> _pendingBySchema = new(StringComparer.Ordinal);

    public OutboxMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);
        _deadLettered = _meter.CreateCounter<long>("fincore.outbox.dead_lettered", description: "Outbox messages moved to dead-letter.");
        _meter.CreateObservableGauge(
            "fincore.outbox.pending",
            () => _pendingBySchema.Select(p => new Measurement<long>(p.Value, new KeyValuePair<string, object?>("module.schema", p.Key))),
            description: "Unprocessed outbox messages per module schema.");
    }

    public void RecordPending(string schema, long pending) => _pendingBySchema[schema] = pending;

    public void RecordDeadLettered(string schema) =>
        _deadLettered.Add(1, new KeyValuePair<string, object?>("module.schema", schema));

    public void Dispose() => _meter.Dispose();
}
