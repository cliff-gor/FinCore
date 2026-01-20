namespace FinCore.SharedKernel.Domain;

/// <summary>Base for entities identified by a strongly-typed id.</summary>
public abstract class Entity<TId>
    where TId : struct
{
    protected Entity(TId id) => Id = id;

    /// <summary>Required by ORMs that materialize entities without calling the domain constructor.</summary>
    protected Entity()
    {
    }

    public TId Id { get; protected init; }
}
