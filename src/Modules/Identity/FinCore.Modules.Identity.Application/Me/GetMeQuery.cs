using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.Modules.Organization.Contracts;
using FinCore.SharedKernel.Results;
using FinCore.SharedKernel.Time;

namespace FinCore.Modules.Identity.Application.Me;

public sealed record MeInstitution(Guid Id, string Code, string Name, string Status);

public sealed record MeBranch(Guid Id, string Code, string Name);

/// <summary>
/// The signed-in user as the clients need it to render the shell (spec 002). Customer fields beyond identity are
/// null in M1; customer records arrive with M2.
/// </summary>
public sealed record MeResponse(
    string Id,
    string Kind,
    string? Name,
    string? Email,
    MeInstitution? Institution,
    MeBranch? HomeBranch,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool AllBranches,
    DateOnly? BusinessDate,
    IReadOnlyList<string> EnabledModules);

/// <param name="Name">Display name from the token (customers); staff names come from FinCore's own record.</param>
public sealed record GetMeQuery(string? Name, string? Email) : IQuery<MeResponse>;

internal sealed class GetMeQueryHandler(
    IStaffRepository staff,
    IOrganizationModuleApi organization,
    IBusinessDateProvider businessDates,
    IEnabledModules enabledModules,
    ICurrentUser currentUser) : IQueryHandler<GetMeQuery, MeResponse>
{
    public async Task<Result<MeResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (currentUser.Kind == UserKind.Customer)
        {
            return new MeResponse(currentUser.Id, "customer", query.Name, query.Email, null, null, [], [], false, null, enabledModules.Names);
        }

        var user = currentUser.StaffId is { } staffId
            ? await staff.GetAsync(new StaffUserId(staffId), cancellationToken).ConfigureAwait(false)
            : null;
        if (user is null)
        {
            return IdentityErrors.StaffNotRegistered;
        }

        MeInstitution? institution = null;
        MeBranch? branch = null;
        DateOnly? businessDate = null;
        if (user.InstitutionId is { } institutionId)
        {
            var org = await organization.GetInstitutionAsync(institutionId, cancellationToken).ConfigureAwait(false);
            institution = org is null ? null : new MeInstitution(org.Id, org.Code, org.Name, org.Status);
            businessDate = await businessDates.GetBusinessDateAsync(institutionId, cancellationToken).ConfigureAwait(false);

            if (user.HomeBranchId is { } branchId)
            {
                var b = await organization.GetBranchAsync(institutionId, branchId, cancellationToken).ConfigureAwait(false);
                branch = b is null ? null : new MeBranch(b.Id, b.Code, b.Name);
            }
        }

        var permissions = user.Permissions.ToList();
        return new MeResponse(
            user.Id.Value.ToString(),
            "staff",
            user.Name,
            user.Email,
            institution,
            branch,
            user.Roles.Select(r => r.Role.ToString()).Order(StringComparer.Ordinal).ToList(),
            permissions,
            permissions.Contains("scope.all-branches"),
            businessDate,
            enabledModules.Names);
    }
}
