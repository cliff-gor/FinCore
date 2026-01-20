namespace FinCore.SharedKernel.Domain;

/// <summary>
/// Marker for value objects. FinCore models value objects as C# records, which already give value equality;
/// this marker only makes intent explicit and lets architecture tests find them.
/// </summary>
public interface IValueObject;
