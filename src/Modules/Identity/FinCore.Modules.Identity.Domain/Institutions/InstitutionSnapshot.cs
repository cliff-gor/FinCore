namespace FinCore.Modules.Identity.Domain.Institutions;

/// <summary>
/// Identity's own copy of an institution's status (table <c>identity.institutions</c>), fed by Organization's events
/// (spec 002). Sign-in and the suspended-write guard read it on every request, so they never call Organization
/// synchronously — and keep working if Organization is extracted into a service (spec §36).
/// </summary>
public sealed class InstitutionSnapshot
{
    /// <summary>
    /// The "as of" time for a copy filled by a synchronous read rather than an event. It is older than any real event,
    /// so every status event still applies on top of it: the read may have run just before a status change committed.
    /// </summary>
    public static readonly DateTime UnknownAsOfUtc = DateTime.UnixEpoch;

    public InstitutionSnapshot(Guid id, string code, string status, string baseCurrency, DateTime updatedAtUtc)
    {
        Id = id;
        Code = code;
        Status = status;
        BaseCurrency = baseCurrency;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    /// <summary><c>Active</c>, <c>Suspended</c> or <c>Closed</c> (Organization's names).</summary>
    public string Status { get; private set; }

    /// <summary>Approval limits are expressed in this currency (spec 002).</summary>
    public string BaseCurrency { get; private set; }

    /// <summary>When the event that set <see cref="Status"/> occurred in Organization (not when Identity stored it).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsClosed => Status == "Closed";

    /// <summary>
    /// Applies a status change unless this copy already reflects a later one. The outbox delivers at least once and
    /// not necessarily in order, so ordering by Organization's event time is what keeps a late "Suspended" from
    /// undoing a "Closed".
    /// </summary>
    /// <returns><see langword="true"/> when the status was applied.</returns>
    public bool ApplyStatus(string status, DateTime occurredOnUtc)
    {
        if (occurredOnUtc <= UpdatedAtUtc)
        {
            return false;
        }

        Status = status;
        UpdatedAtUtc = occurredOnUtc;
        return true;
    }
}
