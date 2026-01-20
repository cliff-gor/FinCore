namespace FinCore.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Describes a module's DbContext so the host (dev migrations, <c>/api/version</c>) and the Migrator can
/// find every module database without referencing module internals.
/// </summary>
public sealed record ModuleDatabase(string ModuleName, Type DbContextType, int MigrationOrder);
