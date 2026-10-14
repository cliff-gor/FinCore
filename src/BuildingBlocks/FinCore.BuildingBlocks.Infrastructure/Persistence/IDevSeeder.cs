namespace FinCore.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Development-only seed data owned by one module (spec 002 AC-22). Each module seeds its own data, because only it
/// may write its schema; the host runs seeders in <see cref="Order"/> after all migrations. Seeders must be idempotent.
/// </summary>
public interface IDevSeeder
{
    /// <summary>Lower runs first: Organization (institutions, branches) before Identity (staff need branches).</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}
