using FinCore.BuildingBlocks.Infrastructure.Persistence.Converters;
using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Modules.Organization.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasConversion(new StronglyTypedIdConverter<BranchId>(v => new BranchId(v)));
        builder.Property(b => b.InstitutionId).HasConversion(new StronglyTypedIdConverter<InstitutionId>(v => new InstitutionId(v)));
        builder.Property(b => b.ParentBranchId)
            .HasConversion(id => id!.Value.Value, v => new BranchId(v));
        builder.Property(b => b.Code)
            .HasConversion(c => c.Value, v => BranchCode.FromStorage(v))
            .HasMaxLength(BranchCode.Length)
            .IsFixedLength();

        // Codes are unique per institution (two banks may both have branch 001); this index is the real guarantee
        // against concurrent duplicates, the handler's check is only a fast path.
        builder.HasIndex(b => new { b.InstitutionId, b.Code }).IsUnique();
        builder.HasIndex(b => b.ParentBranchId);

        // Same schema, so a real foreign key is allowed here (cross-schema ones are not, spec §11).
        builder.HasOne<Institution>().WithMany().HasForeignKey(b => b.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(b => b.ParentBranchId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(b => b.Name).HasMaxLength(Branch.NameMaxLength);
        builder.Property(b => b.Address).HasMaxLength(Branch.AddressMaxLength);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.CreatedBy).HasMaxLength(200);
        builder.Property(b => b.ModifiedBy).HasMaxLength(200);
        builder.Ignore(b => b.DomainEvents);
    }
}
