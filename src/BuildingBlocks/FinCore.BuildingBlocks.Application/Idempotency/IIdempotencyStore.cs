namespace FinCore.BuildingBlocks.Application.Idempotency;

/// <summary>A stored response for an <c>Idempotency-Key</c>, replayed on retries (spec §25).</summary>
public sealed record IdempotentResponse(string RequestHash, int StatusCode, string? ContentType, byte[] Body);

public interface IIdempotencyStore
{
    Task<IdempotentResponse?> GetAsync(string scopedKey, CancellationToken cancellationToken);

    /// <summary>Stores the response; if another request stored the same key first, the call is a no-op.</summary>
    Task SaveAsync(string scopedKey, IdempotentResponse response, CancellationToken cancellationToken);
}
