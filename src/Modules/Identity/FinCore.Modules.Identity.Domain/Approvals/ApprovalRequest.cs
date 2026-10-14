using FinCore.Modules.Identity.Domain.Errors;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Identity.Domain.Approvals;

public readonly record struct ApprovalRequestId(Guid Value) : IStronglyTypedId
{
    public static ApprovalRequestId New() => new(StronglyTypedId.NewValue());
}

/// <summary>
/// <c>Pending → Approved | Rejected | Withdrawn | Expired | Failed</c> (spec 002). <c>Failed</c> means the checkers
/// approved but the change no longer applied when it was executed.
/// </summary>
public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Withdrawn = 4,
    Expired = 5,
    Failed = 6,
}

public enum DecisionKind
{
    Approved = 1,
    Rejected = 2,
}

/// <summary>One checker's decision, kept forever as the four-eyes evidence (table <c>approval_decisions</c>).</summary>
public sealed record ApprovalDecision(Guid CheckerStaffId, DecisionKind Kind, string? Comment, DateTime DecidedAtUtc) : IValueObject;

/// <summary>
/// A maker-checker request (master §13, spec 002). Generic: Identity's own role grants and limit raises use it, and
/// other modules reach it through <c>ApprovalRequested</c> events.
/// </summary>
/// <remarks>
/// The aggregate enforces the rules that need no outside knowledge (state, expiry, the maker never approving, one
/// decision per checker). Whether a checker is <i>eligible</i> — permission, approval limit, institution, not being
/// the subject — needs staff data, so the application layer's approval policy checks that before calling in here.
/// </remarks>
public sealed class ApprovalRequest : AggregateRoot<ApprovalRequestId>
{
    public const int ReasonMaxLength = 500;

    private readonly List<ApprovalDecision> _decisions = [];

    private ApprovalRequest(
        ApprovalRequestId id, Guid? institutionId, string module, string operationType, string entityId, Guid? subjectStaffId,
        decimal? amount, string? currency, string payload, Guid makerStaffId, int requiredCheckers, DateTime expiresAtUtc)
        : base(id)
    {
        InstitutionId = institutionId;
        Module = module;
        OperationType = operationType;
        EntityId = entityId;
        SubjectStaffId = subjectStaffId;
        Amount = amount;
        Currency = currency;
        Payload = payload;
        MakerStaffId = makerStaffId;
        RequiredCheckers = requiredCheckers;
        ExpiresAtUtc = expiresAtUtc;
        Status = ApprovalStatus.Pending;
    }

    /// <summary>EF Core materialization only.</summary>
    private ApprovalRequest()
    {
        Module = null!;
        OperationType = null!;
        EntityId = null!;
        Payload = null!;
    }

    public Guid? InstitutionId { get; private set; }

    /// <summary>Module that owns the operation; <c>Identity</c> for M1's role and limit changes.</summary>
    public string Module { get; private set; }

    public string OperationType { get; private set; }

    /// <summary>The requesting module's id for what is being approved; echoed back in the decision event.</summary>
    public string EntityId { get; private set; }

    /// <summary>Staff whose access the request changes; that person can never decide it (spec 002).</summary>
    public Guid? SubjectStaffId { get; private set; }

    public decimal? Amount { get; private set; }

    public string? Currency { get; private set; }

    /// <summary>JSON shown to checkers. Owned by the requesting module; never contains secrets.</summary>
    public string Payload { get; private set; }

    public Guid MakerStaffId { get; private set; }

    /// <summary>Distinct approvals needed (1, or 2 above the operation's threshold).</summary>
    public int RequiredCheckers { get; private set; }

    /// <summary>Wall-clock expiry (7 days in M1) because the business date cannot advance before EOD exists (spec 002).</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    public ApprovalStatus Status { get; private set; }

    public string? OutcomeReason { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<ApprovalDecision> Decisions => _decisions.AsReadOnly();

    public int ApprovalCount => _decisions.Count(d => d.Kind == DecisionKind.Approved);

    public static ApprovalRequest Create(
        Guid? institutionId, string module, string operationType, string entityId, Guid? subjectStaffId,
        decimal? amount, string? currency, string payload, Guid makerStaffId, int requiredCheckers, DateTime expiresAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requiredCheckers, 1);
        return new ApprovalRequest(
            ApprovalRequestId.New(), institutionId, module, operationType, entityId, subjectStaffId,
            amount, currency, payload, makerStaffId, requiredCheckers, expiresAtUtc);
    }

    /// <summary>
    /// Records an approval. When the required number of distinct checkers is reached the status becomes
    /// <see cref="ApprovalStatus.Approved"/>; the caller must then either <see cref="Complete"/> it or <see cref="Fail"/> it
    /// after trying to execute the change — the decision event is raised by those, with the final outcome.
    /// </summary>
    /// <param name="completesAlone">
    /// True for a PlatformAdmin: their approval satisfies every required checker (spec 002 owner decision), so large
    /// limits can be granted even before anyone else holds enough authority.
    /// </param>
    public Result Approve(Guid checkerStaffId, string? comment, DateTime utcNow, bool completesAlone = false)
    {
        var check = EnsureDecidable(checkerStaffId, utcNow);
        if (check.IsFailure)
        {
            return check;
        }

        _decisions.Add(new ApprovalDecision(checkerStaffId, DecisionKind.Approved, Trim(comment), utcNow));
        if (completesAlone || ApprovalCount >= RequiredCheckers)
        {
            Status = ApprovalStatus.Approved;
        }

        return Result.Success();
    }

    /// <summary>One rejection rejects the whole request (spec 002). A reason is mandatory and shown to the maker.</summary>
    public Result Reject(Guid checkerStaffId, string? reason, DateTime utcNow)
    {
        var trimmed = Trim(reason);
        if (trimmed is null)
        {
            return IdentityErrors.RejectionReasonRequired;
        }

        var check = EnsureDecidable(checkerStaffId, utcNow);
        if (check.IsFailure)
        {
            return check;
        }

        _decisions.Add(new ApprovalDecision(checkerStaffId, DecisionKind.Rejected, trimmed, utcNow));
        Finish(ApprovalStatus.Rejected, trimmed, utcNow);
        return Result.Success();
    }

    /// <summary>The maker changed their mind; only they can withdraw, and only while pending.</summary>
    public Result Withdraw(Guid staffId, DateTime utcNow)
    {
        if (staffId != MakerStaffId)
        {
            return IdentityErrors.OnlyMakerCanWithdraw;
        }

        if (Status != ApprovalStatus.Pending)
        {
            return IdentityErrors.ApprovalAlreadyDecided;
        }

        Finish(ApprovalStatus.Withdrawn, reason: null, utcNow);
        return Result.Success();
    }

    /// <summary>Called by the daily job; a no-op unless the request is pending and past its expiry.</summary>
    public bool ExpireIfDue(DateTime utcNow)
    {
        if (Status != ApprovalStatus.Pending || utcNow < ExpiresAtUtc)
        {
            return false;
        }

        Finish(ApprovalStatus.Expired, reason: null, utcNow);
        return true;
    }

    /// <summary>The approved change was carried out (or, for other modules, handed over); publishes the outcome.</summary>
    public void Complete(DateTime utcNow)
    {
        if (Status != ApprovalStatus.Approved)
        {
            throw new InvalidOperationException("Only an approved request can be completed.");
        }

        Finish(ApprovalStatus.Approved, reason: null, utcNow);
    }

    /// <summary>
    /// The checkers approved but the change no longer applies (e.g. the user was deactivated meanwhile). Nothing is
    /// changed and the maker sees the reason (spec 002).
    /// </summary>
    public void Fail(string reasonCode, DateTime utcNow)
    {
        if (Status != ApprovalStatus.Approved)
        {
            throw new InvalidOperationException("Only an approved request can fail on execution.");
        }

        Finish(ApprovalStatus.Failed, reasonCode, utcNow);
    }

    private Result EnsureDecidable(Guid checkerStaffId, DateTime utcNow)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return IdentityErrors.ApprovalAlreadyDecided;
        }

        // Checked on decision, not only by the daily job, so a request is never approved a minute after expiring.
        if (utcNow >= ExpiresAtUtc)
        {
            return IdentityErrors.ApprovalExpired;
        }

        if (checkerStaffId == MakerStaffId || checkerStaffId == SubjectStaffId)
        {
            return IdentityErrors.FourEyesViolation;
        }

        // Two approvals from the same person must not satisfy a two-checker rule.
        if (_decisions.Any(d => d.CheckerStaffId == checkerStaffId))
        {
            return IdentityErrors.AlreadyDecidedByChecker;
        }

        return Result.Success();
    }

    private void Finish(ApprovalStatus status, string? reason, DateTime utcNow)
    {
        Status = status;
        OutcomeReason = reason;
        CompletedAtUtc = utcNow;
        Raise(new ApprovalRequestDecidedDomainEvent(
            Guid.CreateVersion7(), utcNow, Id, InstitutionId, Module, OperationType, EntityId, status,
            _decisions.Where(d => d.Kind == DecisionKind.Approved || status == ApprovalStatus.Rejected)
                .Select(d => d.CheckerStaffId).ToList(),
            reason));
    }

    private static string? Trim(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > ReasonMaxLength ? trimmed[..ReasonMaxLength] : trimmed;
    }
}

public sealed record ApprovalRequestDecidedDomainEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    ApprovalRequestId RequestId,
    Guid? InstitutionId,
    string Module,
    string OperationType,
    string EntityId,
    ApprovalStatus Outcome,
    IReadOnlyList<Guid> DecidedBy,
    string? Reason) : DomainEvent(EventId, OccurredOnUtc);
