using FinCore.Modules.Identity.Domain.Institutions;

namespace FinCore.Modules.Identity.UnitTests;

/// <summary>Identity's copy of institution status must converge on Organization's latest change (spec 002 AC-10).</summary>
public sealed class InstitutionSnapshotTests
{
    private static readonly DateTime Created = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Status_events_delivered_out_of_order_keep_the_latest_status()
    {
        var snapshot = new InstitutionSnapshot(Guid.CreateVersion7(), "FCB", "Active", "KES", Created);

        snapshot.ApplyStatus("Closed", Created.AddMinutes(2)).ShouldBeTrue();
        snapshot.ApplyStatus("Suspended", Created.AddMinutes(1)).ShouldBeFalse();

        snapshot.Status.ShouldBe("Closed");
    }

    [Fact]
    public void A_redelivered_event_changes_nothing()
    {
        var snapshot = new InstitutionSnapshot(Guid.CreateVersion7(), "FCB", "Active", "KES", Created);
        snapshot.ApplyStatus("Suspended", Created.AddMinutes(1)).ShouldBeTrue();

        snapshot.ApplyStatus("Suspended", Created.AddMinutes(1)).ShouldBeFalse();
    }

    [Fact]
    public void A_copy_filled_by_a_read_accepts_any_status_event()
    {
        // The read may have run just before the change committed, so it must not block the event that follows.
        var snapshot = new InstitutionSnapshot(Guid.CreateVersion7(), "FCB", "Active", "KES", InstitutionSnapshot.UnknownAsOfUtc);

        snapshot.ApplyStatus("Closed", Created).ShouldBeTrue();

        snapshot.IsClosed.ShouldBeTrue();
    }
}
