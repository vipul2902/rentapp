using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Rent;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class RentChargeConfiguration : IEntityTypeConfiguration<RentCharge>
{
    public void Configure(EntityTypeBuilder<RentCharge> builder)
    {
        builder.ToTable("rent_charges", t =>
        {
            t.HasCheckConstraint("ck_rent_charges_amount_positive", "amount > 0");
            t.HasCheckConstraint("ck_rent_charges_paid_non_negative", "paid_amount >= 0");
            t.HasCheckConstraint("ck_rent_charges_adjusted_non_negative", "adjusted_amount >= 0");
            // The core financial invariant: nothing can settle more than is owed.
            t.HasCheckConstraint("ck_rent_charges_not_over_settled", "paid_amount + adjusted_amount <= amount");
            t.HasCheckConstraint("ck_rent_charges_period", "period_end >= period_start");
        });

        builder.Property(c => c.PaidAmount).HasDefaultValue(0m);
        builder.Property(c => c.AdjustedAmount).HasDefaultValue(0m);
        builder.Property(c => c.BalanceAmount)
            .HasComputedColumnSql("amount - paid_amount - adjusted_amount", stored: true);

        builder.HasAlternateKey(c => new { c.Id, c.OrganizationId });

        // Idempotent generation: one charge per tenancy per month, however often the engine runs.
        builder.HasIndex(c => new { c.RentAgreementId, c.PeriodStart }).IsUnique();
        builder.HasIndex(c => new { c.OrganizationId, c.DueDate })
            .HasFilter("cancelled_at IS NULL AND balance_amount > 0")
            .HasDatabaseName("ix_rent_charges_outstanding_by_due_date");
        builder.HasIndex(c => new { c.OrganizationId, c.PeriodStart });
        builder.HasIndex(c => new { c.TenantId, c.DueDate });
        builder.HasIndex(c => new { c.PropertyId, c.DueDate });

        // The charge's tenant and property must be the tenancy's, within one organization.
        builder.HasOne<RentAgreement>()
            .WithMany()
            .HasForeignKey(c => new { c.RentAgreementId, c.TenantId, c.PropertyId, c.OrganizationId })
            .HasPrincipalKey(a => new { a.Id, a.TenantId, a.PropertyId, a.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(c => c.IsCancelled);
    }
}

internal sealed class RentChargeAdjustmentConfiguration : IEntityTypeConfiguration<RentChargeAdjustment>
{
    public void Configure(EntityTypeBuilder<RentChargeAdjustment> builder)
    {
        builder.ToTable("rent_charge_adjustments", t => t.HasCheckConstraint("ck_rent_charge_adjustments_amount_positive", "amount > 0"));

        builder.Property(a => a.Reason).HasMaxLength(RentChargeAdjustment.ReasonMaxLength).IsRequired();
        builder.HasIndex(a => a.RentChargeId);
        builder.HasIndex(a => a.OrganizationId);

        builder.HasOne<RentCharge>()
            .WithMany()
            .HasForeignKey(a => new { a.RentChargeId, a.OrganizationId })
            .HasPrincipalKey(c => new { c.Id, c.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
