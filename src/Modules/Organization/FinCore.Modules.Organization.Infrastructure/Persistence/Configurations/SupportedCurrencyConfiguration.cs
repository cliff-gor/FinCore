using FinCore.Modules.Organization.Domain.Currencies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Modules.Organization.Infrastructure.Persistence.Configurations;

internal sealed class SupportedCurrencyConfiguration : IEntityTypeConfiguration<SupportedCurrency>
{
    public void Configure(EntityTypeBuilder<SupportedCurrency> builder)
    {
        builder.ToTable("currencies");
        builder.HasKey(c => c.IsoCode);
        builder.Property(c => c.IsoCode).HasMaxLength(3).IsFixedLength();
        builder.Property(c => c.Name).HasMaxLength(100);

        // Reference data ships with the migration so every environment has the same currencies.
        builder.HasData(SupportedCurrency.Seed);
    }
}
