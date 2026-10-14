using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Staff;
using Microsoft.Extensions.Options;

namespace FinCore.Modules.Identity.Application.Approvals;

/// <summary>How one kind of operation is approved (spec 002 "multiple checkers").</summary>
public sealed class ApprovalOperationOptions
{
    /// <summary>Permission a checker must hold, e.g. <c>ledger.journal.approve</c>.</summary>
    public string Permission { get; set; } = "identity.approve";

    /// <summary>Limit the checker's own authority is compared with, if the operation carries an amount.</summary>
    public ApprovalLimitType? LimitType { get; set; }

    public int RequiredCheckers { get; set; } = 1;

    /// <summary>Above this amount, two distinct checkers are required.</summary>
    public decimal? SecondCheckerThreshold { get; set; }
}

/// <summary>Configuration section <c>Approvals</c>.</summary>
public sealed class ApprovalOptions
{
    public const string SectionName = "Approvals";

    /// <summary>Pending requests expire after this many calendar days (spec 002: 7).</summary>
    public int ExpiryDays { get; set; } = 7;

    /// <summary>Raising a limit above this needs two checkers (spec 002 default: 1,000,000 in base currency).</summary>
    public decimal LimitRaiseSecondCheckerThreshold { get; set; } = 1_000_000m;

    /// <summary>Operations of other modules, keyed by operation type (e.g. <c>Ledger.ManualJournal</c>).</summary>
    public Dictionary<string, ApprovalOperationOptions> Operations { get; } = new(StringComparer.Ordinal);
}

/// <summary>Resolves the approval rules for an operation type: Identity's own built in, others from configuration.</summary>
internal sealed class ApprovalOperationCatalog(IOptions<ApprovalOptions> options)
{
    public ApprovalOperationOptions? Find(string operationType) => operationType switch
    {
        IdentityOperations.GrantRole => new ApprovalOperationOptions { Permission = "identity.approve" },
        IdentityOperations.RaiseApprovalLimit => new ApprovalOperationOptions
        {
            Permission = "identity.approve",
            SecondCheckerThreshold = options.Value.LimitRaiseSecondCheckerThreshold,
        },
        _ => options.Value.Operations.GetValueOrDefault(operationType),
    };

    public static int RequiredCheckers(ApprovalOperationOptions operation, decimal? amount) =>
        operation.SecondCheckerThreshold is { } threshold && amount > threshold
            ? Math.Max(2, operation.RequiredCheckers)
            : operation.RequiredCheckers;

    public TimeSpan Expiry => TimeSpan.FromDays(options.Value.ExpiryDays);
}
