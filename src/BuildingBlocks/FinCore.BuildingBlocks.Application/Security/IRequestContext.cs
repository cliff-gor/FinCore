namespace FinCore.BuildingBlocks.Application.Security;

/// <summary>Per-request facts that are not about the user (spec §27): currently the correlation id for audit and logs.</summary>
public interface IRequestContext
{
    string? CorrelationId { get; }
}
