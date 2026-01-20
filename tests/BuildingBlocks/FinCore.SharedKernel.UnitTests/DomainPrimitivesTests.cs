using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Time;

namespace SharedKernelTests;

public sealed class DomainPrimitivesTests
{
    private sealed record TestEvent(Guid EventId, DateTime OccurredOnUtc) : DomainEvent(EventId, OccurredOnUtc);

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id)
            : base(id)
        {
        }

        public void DoSomething() => Raise(new TestEvent(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Aggregate_collects_and_clears_domain_events()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.DoSomething();
        aggregate.DomainEvents.Count.ShouldBe(1);

        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Aggregate_audit_fields_are_set_through_IAuditable()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        IAuditable auditable = aggregate;
        var created = new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc);

        auditable.SetCreated(created, "maker");
        auditable.SetModified(created.AddHours(1), "checker");

        aggregate.CreatedAtUtc.ShouldBe(created);
        aggregate.CreatedBy.ShouldBe("maker");
        aggregate.ModifiedAtUtc.ShouldBe(created.AddHours(1));
        aggregate.ModifiedBy.ShouldBe("checker");
    }

    [Fact]
    public void New_ids_are_version_7()
    {
        StronglyTypedId.NewValue().Version.ShouldBe(7);
    }

    [Fact]
    public void Date_range_is_inclusive()
    {
        var range = new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        range.Days.ShouldBe(31);
        range.Contains(new DateOnly(2026, 1, 31)).ShouldBeTrue();
        range.Contains(new DateOnly(2026, 2, 1)).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => new DateRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
    }
}
