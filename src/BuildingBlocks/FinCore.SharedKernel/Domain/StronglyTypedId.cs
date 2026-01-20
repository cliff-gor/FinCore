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
    public static Guid NewValue() => Guid.CreateVersion7();
}
