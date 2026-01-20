namespace FinCore.SharedKernel.Domain;

/// <summary>
/// Implemented by <c>readonly record struct XxxId(Guid Value)</c> ids so persistence and serialization can
/// treat them uniformly. New ids are time-ordered (UUID v7) for index locality.
/// </summary>
public interface IStronglyTypedId
{
    Guid Value { get; }
}

public static class StronglyTypedId
{
    /// <summary>
    /// New UUID v7. Time-ordered ids keep B-tree inserts at the end of the index instead of scattering them
    /// like random v4 GUIDs do.
    /// </summary>
    public static Guid NewValue() => Guid.CreateVersion7();
}
