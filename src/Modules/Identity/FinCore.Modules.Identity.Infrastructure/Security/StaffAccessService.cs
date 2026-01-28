using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Staff;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace FinCore.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Resolves a staff token's subject to FinCore access, linking the IdP account on first sign-in (spec 002).
/// Access profiles are cached for 5 minutes and cleared explicitly on every change (<see cref="IAccessCache"/>), so
/// the cache saves a query per request without ever delaying a revocation.
/// </summary>
internal sealed class StaffAccessService(
    HybridCache cache,
    IStaffRepository staff,
    IInstitutionDirectory institutions,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<StaffAccessService> logger) : IAccessCache
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    // Read-only probe: returns the cached value if present and never stores the factory's null.
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableDistributedCacheWrite,
    };

    public static string Key(string subject) => $"identity:access:{subject}";

    public static string StaffTag(Guid staffId) => $"identity:staff:{staffId}";

    public static string InstitutionTag(Guid institutionId) => $"identity:institution:{institutionId}";

    public async Task<StaffAccess?> ResolveAsync(string subject, string? email, bool emailVerified, CancellationToken cancellationToken)
    {
        var cached = await cache.GetOrCreateAsync<StaffAccess?>(
            Key(subject), _ => ValueTask.FromResult<StaffAccess?>(null), ProbeOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var user = await staff.GetBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            ?? await LinkOnFirstSignInAsync(subject, email, emailVerified, cancellationToken).ConfigureAwait(false);

        // "Not registered" and "email not verified" are not cached: an admin may register the person a minute later,
        // and they should be able to sign in straight away.
        if (user is null)
        {
            return null;
        }

        var access = await BuildAsync(user, cancellationToken).ConfigureAwait(false);
        var tags = access.InstitutionId is { } institution
            ? new[] { StaffTag(user.Id.Value), InstitutionTag(institution) }
            : [StaffTag(user.Id.Value)];
        await cache.SetAsync(Key(subject), access, CacheOptions, tags, cancellationToken).ConfigureAwait(false);
        return access;
    }

    public Task InvalidateStaffAsync(Guid staffId, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(StaffTag(staffId), cancellationToken).AsTask();

    public Task InvalidateInstitutionAsync(Guid institutionId, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(InstitutionTag(institutionId), cancellationToken).AsTask();

    private async Task<StaffUser?> LinkOnFirstSignInAsync(string subject, string? email, bool emailVerified, CancellationToken cancellationToken)
    {
        // Linking by an unverified email would let anyone who registers that address at the IdP take over the
        // pre-registered account, so it is refused outright.
        if (!emailVerified || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var user = await staff.GetByEmailAsync(StaffUser.NormalizeEmail(email), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        // Already linked: either by a concurrent first request of this same sign-in (use it) or to a different IdP
        // account (refuse — one staff user maps to exactly one sign-in identity).
        if (user.ExternalSubjectId is not null)
        {
            return user.ExternalSubjectId == subject ? user : null;
        }

        var linked = user.LinkIdentity(subject, timeProvider.GetUtcNow().UtcDateTime);
        if (linked.IsFailure)
        {
            return user;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ConcurrencyConflictException or UniqueConstraintViolationException)
        {
            // A browser fires several requests at once on first sign-in; one of them linked the account first.
            // Use that result instead of failing this request.
            logger.LogDebug(ex, "Staff user {StaffId} was linked by a concurrent request", user.Id.Value);
            unitOfWork.DiscardChanges();
            return await staff.GetBySubjectAsync(subject, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Linked staff user {StaffId} to its sign-in account on first sign-in", user.Id.Value);
        return user;
    }

    private async Task<StaffAccess> BuildAsync(StaffUser user, CancellationToken cancellationToken)
    {
        string? institutionStatus = null;
        if (user.InstitutionId is { } institutionId)
        {
            institutionStatus = (await institutions.GetAsync(institutionId, cancellationToken).ConfigureAwait(false))?.Status;
        }

        // Deactivated staff and staff of a Closed institution cannot use FinCore at all (spec 002 AC-10).
        var inactive = user.Status == StaffStatus.Deactivated || institutionStatus == "Closed";
        return new StaffAccess(
            user.Id.Value,
            user.InstitutionId,
            institutionStatus,
            user.HomeBranchId,
            inactive ? [] : user.Permissions.ToList(),
            inactive ? IdentityErrors.StaffInactive.Code : null);
    }
}
