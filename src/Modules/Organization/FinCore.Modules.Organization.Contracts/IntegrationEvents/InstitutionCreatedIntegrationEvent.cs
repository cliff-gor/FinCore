using FinCore.SharedKernel.Integration;

namespace FinCore.Modules.Organization.Contracts.IntegrationEvents;

/// <summary>Published when an institution is created. Subscribers (M1+): Ledger seeds the chart of accounts, Identity.</summary>
public sealed record InstitutionCreatedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid InstitutionId,
    string Code,
    string Name,
    string Type,
    string BaseCurrency,
    DateOnly BusinessDate) : IIntegrationEvent;
