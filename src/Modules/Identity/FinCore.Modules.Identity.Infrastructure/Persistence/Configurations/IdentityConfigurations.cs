using FinCore.BuildingBlocks.Infrastructure.Persistence.Converters;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Institutions;
using FinCore.Modules.Identity.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.ToTable("staff_users");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(new StronglyTypedIdConverter<StaffUserId>(v => new StaffUserId(v)));

        // Emails are stored lower-cased by the aggregate, so a plain unique index is case-insensitive in practice:
        // one email = one staff user platform-wide (spec 002).
        builder.Property(s => s.Email).HasMaxLength(StaffUser.EmailMaxLength);
        builder.HasIndex(s => s.Email).IsUnique();

        // One IdP account can be linked to at most one staff user. The filter allows many not-yet-linked users.
        builder.Property(s => s.ExternalSubjectId).HasMaxLength(200);
        builder.HasIndex(s => s.ExternalSubjectId).IsUnique().HasFilter("external_subject_id IS NOT NULL");

        builder.Property(s => s.Name).HasMaxLength(StaffUser.NameMaxLength);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(s => new { s.InstitutionId, s.HomeBranchId });
        builder.Property(s => s.CreatedBy).HasMaxLength(200);
        builder.Property(s => s.ModifiedBy).HasMaxLength(200);

        builder.OwnsMany(s => s.Roles, r =>
        {
            r.ToTable("staff_roles");
            r.WithOwner().HasForeignKey("staff_user_id");
            // Surrogate key: with a natural key EF would treat a newly added role as an existing row (UPDATE, not
            // INSERT). The unique index keeps "a role at most once per user" enforced by the database.
            r.Property<long>("id").ValueGeneratedOnAdd();
            r.HasKey("id");
            r.HasIndex("staff_user_id", nameof(StaffRole.Role)).IsUnique();
            r.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
        });
        builder.Navigation(s => s.Roles).HasField("_roles").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(s => s.ApprovalLimits, l =>
        {
            l.ToTable("approval_limits");
            l.WithOwner().HasForeignKey("staff_user_id");
            // Surrogate key for the same reason as roles; one limit per type per user stays unique.
            l.Property<long>("id").ValueGeneratedOnAdd();
            l.HasKey("id");
            l.HasIndex("staff_user_id", nameof(ApprovalLimit.Type)).IsUnique();
            l.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            // Money columns are numeric(19,4) everywhere (ADR-0004).
            l.Property(x => x.MaxAmount).HasPrecision(19, 4);
        });
        builder.Navigation(s => s.ApprovalLimits).HasField("_approvalLimits").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.Permissions);
        builder.Ignore(s => s.IsActive);
        builder.Ignore(s => s.IsPlatformAdmin);
    }
}

internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(new StronglyTypedIdConverter<ApprovalRequestId>(v => new ApprovalRequestId(v)));
        builder.Property(r => r.Module).HasMaxLength(50);
        builder.Property(r => r.OperationType).HasMaxLength(100);
        builder.Property(r => r.EntityId).HasMaxLength(100);
        builder.Property(r => r.Amount).HasPrecision(19, 4);
        builder.Property(r => r.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(r => r.Payload).HasColumnType("jsonb");
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.OutcomeReason).HasMaxLength(ApprovalRequest.ReasonMaxLength);
        builder.Property(r => r.CreatedBy).HasMaxLength(200);
        builder.Property(r => r.ModifiedBy).HasMaxLength(200);

        // The inbox and the expiry job both look for pending requests.
        builder.HasIndex(r => new { r.Status, r.InstitutionId });
        builder.HasIndex(r => new { r.Status, r.ExpiresAtUtc });
        builder.HasIndex(r => r.MakerStaffId);
        builder.HasIndex(r => new { r.Module, r.OperationType, r.EntityId });

        builder.OwnsMany(r => r.Decisions, d =>
        {
            d.ToTable("approval_decisions");
            d.WithOwner().HasForeignKey("approval_request_id");
            d.Property<long>("id").ValueGeneratedOnAdd();
            d.HasKey("id");
            // One decision per checker per request: the database backs up the aggregate's four-eyes rule.
            d.HasIndex("approval_request_id", nameof(ApprovalDecision.CheckerStaffId)).IsUnique();
            d.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            d.Property(x => x.Comment).HasMaxLength(ApprovalRequest.ReasonMaxLength);
        });
        builder.Navigation(r => r.Decisions).HasField("_decisions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.DomainEvents);
        builder.Ignore(r => r.ApprovalCount);
    }
}

internal sealed class InstitutionSnapshotConfiguration : IEntityTypeConfiguration<InstitutionSnapshot>
{
    public void Configure(EntityTypeBuilder<InstitutionSnapshot> builder)
    {
        builder.ToTable("institutions");
        builder.HasKey(i => i.Id);
        // Not generated here: the id is Organization's institution id.
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Code).HasMaxLength(6);
        builder.Property(i => i.Status).HasMaxLength(20);
        builder.Property(i => i.BaseCurrency).HasMaxLength(3);
        builder.Ignore(i => i.IsClosed);
    }
}
