namespace FinCore.Modules.Organization.Domain.Institutions;

/// <summary>Institutions are created Active (spec 001); transitions arrive with M1.</summary>
public enum InstitutionStatus
{
    Active = 1,
    Suspended = 2,
    Closed = 3,
}
