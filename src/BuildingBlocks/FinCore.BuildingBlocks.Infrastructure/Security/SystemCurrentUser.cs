using FinCore.BuildingBlocks.Application.Security;

namespace FinCore.BuildingBlocks.Infrastructure.Security;

/// <summary>Identity used outside HTTP requests (jobs, event handlers, seeders).</summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public string Id => "system";

    public UserKind Kind => UserKind.System;

    public Guid? StaffId => null;

    public Guid? InstitutionId => null;

    public Guid? HomeBranchId => null;

    public bool HasAllBranches => false;

    public bool IsPlatformAdmin => false;

    public bool HasPermission(string permission) => false;
}

/// <summary>Request context for hosts without HTTP requests (Migrator, background jobs).</summary>
internal sealed class NoRequestContext : IRequestContext
{
    public string? CorrelationId => null;
}
