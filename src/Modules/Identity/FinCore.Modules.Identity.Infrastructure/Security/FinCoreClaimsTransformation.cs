using System.Security.Claims;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.BuildingBlocks.Web.Authorization;
using FinCore.BuildingBlocks.Web.Security;
using FinCore.Modules.Identity.Domain.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace FinCore.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Adds FinCore's authorization data to a validated token (spec 002). The identity provider only proves who the user
/// is; roles, permissions, institution and branch come from Identity's database, so access can be revoked instantly
/// without waiting for tokens to expire.
/// </summary>
internal sealed class FinCoreClaimsTransformation(StaffAccessService accessService, IConfiguration configuration) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Authentication can run more than once per request; enrich only once.
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(c => c.Type == FinCoreClaimTypes.Kind))
        {
            return principal;
        }

        var identity = new ClaimsIdentity();
        var issuer = principal.FindFirst("iss")?.Value.TrimEnd('/');
        var customerIssuer = FinCoreAuthentication.AuthPool.Read(configuration, FinCoreAuthentication.CustomerScheme).Issuer;

        if (customerIssuer is not null && string.Equals(issuer, customerIssuer, StringComparison.Ordinal))
        {
            // M1 customers are identity-only: no permissions, so every staff endpoint rejects them (spec 002 AC-4).
            identity.AddClaim(new Claim(FinCoreClaimTypes.Kind, nameof(UserKind.Customer)));
            principal.AddIdentity(identity);
            return principal;
        }

        identity.AddClaim(new Claim(FinCoreClaimTypes.Kind, nameof(UserKind.Staff)));
        var subject = principal.FindFirst("sub")?.Value;
        var email = principal.FindFirst("email")?.Value;
        var emailVerified = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        var access = subject is null
            ? null
            : await accessService.ResolveAsync(subject, email, emailVerified, CancellationToken.None).ConfigureAwait(false);

        if (access is null)
        {
            // No permissions are added; /me reports the reason so the Back Office can show "contact your administrator".
            var code = !emailVerified ? IdentityErrors.EmailNotVerified.Code : IdentityErrors.StaffNotRegistered.Code;
            identity.AddClaim(new Claim(FinCoreClaimTypes.AccessDenied, code));
        }
        else if (access.DeniedCode is not null)
        {
            identity.AddClaim(new Claim(FinCoreClaimTypes.AccessDenied, access.DeniedCode));
            identity.AddClaim(new Claim(FinCoreClaimTypes.StaffId, access.StaffId.ToString()));
        }
        else
        {
            identity.AddClaim(new Claim(FinCoreClaimTypes.StaffId, access.StaffId.ToString()));
            if (access.InstitutionId is { } institution)
            {
                identity.AddClaim(new Claim(FinCoreClaimTypes.InstitutionId, institution.ToString()));
            }

            if (access.InstitutionStatus is { } status)
            {
                identity.AddClaim(new Claim(FinCoreClaimTypes.InstitutionStatus, status));
            }

            if (access.HomeBranchId is { } branch)
            {
                identity.AddClaim(new Claim(FinCoreClaimTypes.HomeBranchId, branch.ToString()));
            }

            foreach (var permission in access.Permissions)
            {
                identity.AddClaim(new Claim(Permissions.ClaimType, permission));
            }
        }

        principal.AddIdentity(identity);
        return principal;
    }
}
