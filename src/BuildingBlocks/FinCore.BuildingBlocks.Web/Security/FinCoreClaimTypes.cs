namespace FinCore.BuildingBlocks.Web.Security;

/// <summary>
/// Claims FinCore adds to a validated token (Identity's claims transformation, spec 002). The identity provider
/// only proves who the user is; everything below comes from FinCore's own database.
/// </summary>
public static class FinCoreClaimTypes
{
    public const string Kind = "fincore:kind";
    public const string StaffId = "fincore:staff_id";
    public const string InstitutionId = "fincore:institution";
    public const string InstitutionStatus = "fincore:institution_status";
    public const string HomeBranchId = "fincore:branch";

    /// <summary>Set when the IdP account could not be linked to an active staff user; carries the error code.</summary>
    public const string AccessDenied = "fincore:access_denied";
}
