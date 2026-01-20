using FinCore.BuildingBlocks.Infrastructure.Persistence.Converters;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Modules.Organization.Infrastructure.Persistence.Configurations;

internal sealed class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institutions");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasConversion(new StronglyTypedIdConverter<InstitutionId>(v => new InstitutionId(v)));

        builder.Property(i => i.Code)
            .HasConversion(c => c.Value, v => InstitutionCode.FromStorage(v))
            .HasMaxLength(InstitutionCode.MaxLength);
        builder.HasIndex(i => i.Code).IsUnique();

        builder.Property(i => i.Name).HasMaxLength(Institution.NameMaxLength);
        builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.BaseCurrency).HasMaxLength(3).IsFixedLength();
        builder.Property(i => i.LicenseNumber).HasMaxLength(Institution.LicenseNumberMaxLength);
        builder.Property(i => i.CreatedBy).HasMaxLength(200);
        builder.Property(i => i.ModifiedBy).HasMaxLength(200);
        builder.Ignore(i => i.DomainEvents);
    }
}
