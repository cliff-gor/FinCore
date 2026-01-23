namespace FinCore.Modules.Identity.Domain.Institutions;

/// <summary>
/// Identity's own copy of an institution's status (table <c>identity.institutions</c>), fed by Organization's events
/// (spec 002). Sign-in and the suspended-write guard read it on every request, so they never call Organization
/// synchronously — and keep working if Organization is extracted into a service (spec §36).
/// </summary>
public sealed class InstitutionSnapshot
{
    public InstitutionSnapshot(Guid id, string code, string status, string baseCurrency)
    {
        Id = id;
        Code = code;
        Status = status;
        BaseCurrency = baseCurrency;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    /// <summary><c>Active</c>, <c>Suspended</c> or <c>Closed</c> (Organization's names).</summary>
    public string Status { get; private set; }

    /// <summary>Approval limits are expressed in this currency (spec 002).</summary>
    public string BaseCurrency { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsClosed => Status == "Closed";

    public void ChangeStatus(string status, DateTime utcNow)
    {
        Status = status;
        UpdatedAtUtc = utcNow;
    }
}
