using FinCore.SharedKernel.Domain;

namespace FinCore.SharedKernel.Time;

/// <summary>Inclusive date range.</summary>
public sealed record DateRange : IValueObject
{
    public DateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentException("Range end must not be before its start.", nameof(to));
        }

        From = from;
        To = to;
    }

    public DateOnly From { get; }

    public DateOnly To { get; }

    public int Days => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= From && date <= To;
}
