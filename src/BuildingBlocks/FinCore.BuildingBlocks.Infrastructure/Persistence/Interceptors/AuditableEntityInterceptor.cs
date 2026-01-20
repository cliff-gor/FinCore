using FinCore.BuildingBlocks.Application.Security;
using FinCore.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinCore.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>Fills audit columns from <see cref="ICurrentUser"/> and <see cref="TimeProvider"/> (UTC).</summary>
public sealed class AuditableEntityInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            // One timestamp per SaveChanges, so all rows written together share the same audit time.
            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var entry in eventData.Context.ChangeTracker.Entries<IAuditable>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.SetCreated(now, currentUser.Id);
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.SetModified(now, currentUser.Id);
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
