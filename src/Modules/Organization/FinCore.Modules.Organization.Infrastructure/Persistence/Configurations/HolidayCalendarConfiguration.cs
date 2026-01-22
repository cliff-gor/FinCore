using FinCore.BuildingBlocks.Infrastructure.Persistence.Converters;
using FinCore.Modules.Organization.Domain.Calendars;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Modules.Organization.Infrastructure.Persistence.Configurations;

internal sealed class HolidayCalendarConfiguration : IEntityTypeConfiguration<HolidayCalendar>
{
    public void Configure(EntityTypeBuilder<HolidayCalendar> builder)
    {
        builder.ToTable("holiday_calendars");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("institution_id")
            .HasConversion(new StronglyTypedIdConverter<InstitutionId>(v => new InstitutionId(v)));
        builder.HasOne<Institution>().WithOne().HasForeignKey<HolidayCalendar>(c => c.Id).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(c => c.InstitutionId);

        // Weekend days are a tiny fixed set, stored as a comma-separated list of ISO day numbers ("6,0").
        // The comparer makes EF notice in-place changes to the list.
        builder.Property(c => c.WeekendDays)
            .HasConversion(
                days => string.Join(',', days.Select(d => (int)d)),
                text => text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => (DayOfWeek)int.Parse(d, System.Globalization.CultureInfo.InvariantCulture)).ToList(),
                new ValueComparer<IReadOnlyList<DayOfWeek>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, d) => HashCode.Combine(h, d)),
                    v => v.ToList()))
            .HasMaxLength(20);

        builder.OwnsMany(c => c.Holidays, h =>
        {
            h.ToTable("holidays");
            h.WithOwner().HasForeignKey("institution_id");
            // Surrogate key: replacing holidays deletes and re-adds rows with the same date, which a natural key would
            // turn into a tracking conflict. One holiday per date per institution stays unique in the database.
            h.Property<long>("id").ValueGeneratedOnAdd();
            h.HasKey("id");
            h.HasIndex("institution_id", nameof(Holiday.Date)).IsUnique();
            h.Property(x => x.Name).HasMaxLength(HolidayCalendar.HolidayNameMaxLength);
        });
        builder.Navigation(c => c.Holidays).HasField("_holidays").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(c => c.CreatedBy).HasMaxLength(200);
        builder.Property(c => c.ModifiedBy).HasMaxLength(200);
        builder.Ignore(c => c.DomainEvents);
    }
}
