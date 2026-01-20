namespace FinCore.SharedKernel.Time;

/// <summary>
/// The institution's accounting date (owned by the Organization module). Financial logic uses this,
/// never the wall clock. It advances only when End of Day completes.
/// </summary>
public interface IBusinessDateProvider
{
    ValueTask<DateOnly?> GetBusinessDateAsync(Guid institutionId, CancellationToken cancellationToken);
}
