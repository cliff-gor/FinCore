namespace FinCore.BuildingBlocks.Application.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable identifier for audit columns: the IdP subject, or <c>system</c> for background work.</summary>
    string Id { get; }

    bool HasPermission(string permission);
}
