using FinCore.BuildingBlocks.Application.Security;

namespace FinCore.BuildingBlocks.Infrastructure.Security;

/// <summary>Identity used outside HTTP requests (jobs, event handlers, seeders).</summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public string Id => "system";

    public bool HasPermission(string permission) => false;
}
