using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Organizations;
using RentApp.Domain.Payments;
using RentApp.Domain.Rent;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public const string IdempotencyKeyIndex = "ux_payments_organization_id_idempotency_key";

    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", t =>
        {
            t.HasCheckConstraint("ck_payments_amount_positive", "amount > 0");
            t.HasCheckConstraint("ck_payments_status", "status IN ('Recorded', 'Voided')");
            t.HasCheckConstraint("ck_payments_method", "method IN ('Cash', 'Upi', 'BankTransfer', 'Card', 'Other')");
            // A voided payment always says when and why.
            t.HasCheckConstraint("ck_payments_voided_has_details", "(status = 'Voided') = (voided_at IS NOT NULL AND void_reason IS NOT NULL)");
        });

        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ReferenceNumber).HasMaxLength(Payment.ReferenceMaxLength);
        builder.Property(p => p.Notes).HasMaxLength(Payment.NotesMaxLength);
        builder.Property(p => p.VoidReason).HasMaxLength(Payment.VoidReasonMaxLength);
        builder.Property(p => p.IdempotencyKey).HasMaxLength(Payment.IdempotencyKeyMaxLength);

        builder.HasAlternateKey(p => new { p.Id, p.OrganizationId });
        // A retried request can never create a second payment, even when two retries race.
        builder.HasIndex(p => new { p.OrganizationId, p.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName(IdempotencyKeyIndex);
        builder.HasIndex(p => new { p.OrganizationId, p.PaymentDate });
        builder.HasIndex(p => new { p.TenantId, p.PaymentDate });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => new { p.TenantId, p.OrganizationId })
            .HasPrincipalKey(t => new { t.Id, t.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(p => p.IsVoided);
    }
}

internal sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("payment_allocations", t => t.HasCheckConstraint("ck_payment_allocations_amount_positive", "allocated_amount > 0"));

        builder.HasIndex(a => new { a.PaymentId, a.RentChargeId }).IsUnique();
        builder.HasIndex(a => a.RentChargeId);
        builder.HasIndex(a => a.OrganizationId);

        // Both sides must be in the allocation's organization.
        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(a => new { a.PaymentId, a.OrganizationId })
            .HasPrincipalKey(p => new { p.Id, p.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RentCharge>()
            .WithMany()
            .HasForeignKey(a => new { a.RentChargeId, a.OrganizationId })
            .HasPrincipalKey(c => new { c.Id, c.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public const string NumberIndex = "ux_receipts_organization_id_receipt_number";

    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts", t =>
        {
            t.HasCheckConstraint("ck_receipts_amount_positive", "amount > 0");
            t.HasCheckConstraint("ck_receipts_method", "method IN ('Cash', 'Upi', 'BankTransfer', 'Card', 'Other')");
        });

        builder.Property(r => r.ReceiptNumber).HasMaxLength(Receipt.NumberMaxLength).IsRequired();
        builder.Property(r => r.OrganizationName).HasMaxLength(Organization.NameMaxLength).IsRequired();
        builder.Property(r => r.PropertyName).HasMaxLength(Domain.Properties.Property.NameMaxLength).IsRequired();
        builder.Property(r => r.PropertyAddress).HasMaxLength(600).IsRequired();
        builder.Property(r => r.PropertyContactPhone).HasMaxLength(Domain.Properties.Property.PhoneMaxLength);
        builder.Property(r => r.TenantName).HasMaxLength(Tenant.NameMaxLength).IsRequired();
        builder.Property(r => r.TenantPhone).HasMaxLength(Tenant.PhoneMaxLength).IsRequired();
        builder.Property(r => r.RoomNumber).HasMaxLength(Domain.Properties.Room.NumberMaxLength).IsRequired();
        builder.Property(r => r.BedLabel).HasMaxLength(Domain.Properties.Bed.LabelMaxLength).IsRequired();
        builder.Property(r => r.PeriodLabel).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ReferenceNumber).HasMaxLength(Payment.ReferenceMaxLength);

        builder.HasIndex(r => new { r.OrganizationId, r.ReceiptNumber }).IsUnique().HasDatabaseName(NumberIndex);
        builder.HasIndex(r => r.PaymentId).IsUnique();

        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(r => new { r.PaymentId, r.OrganizationId })
            .HasPrincipalKey(p => new { p.Id, p.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReceiptCounterConfiguration : IEntityTypeConfiguration<ReceiptCounter>
{
    public void Configure(EntityTypeBuilder<ReceiptCounter> builder)
    {
        builder.ToTable("receipt_counters", t => t.HasCheckConstraint("ck_receipt_counters_last_number_positive", "last_number > 0"));
        builder.HasKey(c => new { c.OrganizationId, c.Year });
        builder.HasOne<Organization>().WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
