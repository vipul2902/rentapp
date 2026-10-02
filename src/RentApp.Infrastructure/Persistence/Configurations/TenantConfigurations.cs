using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Organizations;
using RentApp.Domain.Properties;
using RentApp.Domain.Tenants;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", t => t.HasCheckConstraint("ck_tenants_status", "status IN ('Active', 'Archived')"));

        builder.Property(t => t.FullName).HasMaxLength(Tenant.NameMaxLength).IsRequired();
        builder.Property(t => t.Phone).HasMaxLength(Tenant.PhoneMaxLength).IsRequired();
        builder.Property(t => t.PhoneDigits).HasMaxLength(Tenant.PhoneMaxLength).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(Tenant.EmailMaxLength);
        builder.Property(t => t.EmergencyContactName).HasMaxLength(Tenant.NameMaxLength);
        builder.Property(t => t.EmergencyContactPhone).HasMaxLength(Tenant.PhoneMaxLength);
        builder.Property(t => t.PermanentAddress).HasMaxLength(Tenant.AddressMaxLength);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasAlternateKey(t => new { t.Id, t.OrganizationId });
        builder.HasIndex(t => new { t.OrganizationId, t.Status, t.FullName });
        builder.HasIndex(t => new { t.OrganizationId, t.PhoneDigits });

        builder.HasOne<Organization>().WithMany().HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(t => t.IsArchived);
    }
}

internal sealed class RentAgreementConfiguration : IEntityTypeConfiguration<RentAgreement>
{
    public const string ActiveBedIndex = "ux_rent_agreements_active_bed_id";
    public const string ActiveTenantIndex = "ux_rent_agreements_active_tenant_id";

    public void Configure(EntityTypeBuilder<RentAgreement> builder)
    {
        builder.ToTable("rent_agreements", t =>
        {
            t.HasCheckConstraint("ck_rent_agreements_status", "status IN ('Active', 'Ended')");
            t.HasCheckConstraint("ck_rent_agreements_end_reason", "end_reason IS NULL OR end_reason IN ('MovedOut', 'Transferred', 'Cancelled')");
            t.HasCheckConstraint("ck_rent_agreements_ended_has_end_date", "(status = 'Ended') = (end_date IS NOT NULL)");
            t.HasCheckConstraint("ck_rent_agreements_ended_has_reason", "(status = 'Ended') = (end_reason IS NOT NULL)");
            t.HasCheckConstraint("ck_rent_agreements_dates", "end_date IS NULL OR end_date >= start_date");
            t.HasCheckConstraint("ck_rent_agreements_rent_positive", "monthly_rent > 0");
            t.HasCheckConstraint("ck_rent_agreements_deposit_non_negative", "security_deposit >= 0");
            t.HasCheckConstraint("ck_rent_agreements_due_day", $"rent_due_day BETWEEN {RentAgreement.MinDueDay} AND {RentAgreement.MaxDueDay}");
        });

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.EndReason).HasConversion<string>().HasMaxLength(20);

        // The heart of "no double booking": at most one active tenancy per bed and per tenant, enforced by
        // the database so that concurrent requests cannot both succeed.
        builder.HasIndex(a => a.BedId).IsUnique().HasFilter("status = 'Active'").HasDatabaseName(ActiveBedIndex);
        builder.HasIndex(a => a.TenantId).IsUnique().HasFilter("status = 'Active'").HasDatabaseName(ActiveTenantIndex);
        builder.HasIndex(a => new { a.OrganizationId, a.Status });
        builder.HasIndex(a => new { a.PropertyId, a.Status });
        builder.HasIndex(a => new { a.RoomId, a.Status });

        // Composite keys make the stored property/room/bed/tenant combination internally consistent and
        // within one organization: the bed must be in the room, the room in the property.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => new { a.TenantId, a.OrganizationId })
            .HasPrincipalKey(t => new { t.Id, t.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bed>()
            .WithMany()
            .HasForeignKey(a => new { a.BedId, a.RoomId, a.OrganizationId })
            .HasPrincipalKey(b => new { b.Id, b.RoomId, b.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(a => new { a.RoomId, a.PropertyId, a.OrganizationId })
            .HasPrincipalKey(r => new { r.Id, r.PropertyId, r.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(a => a.IsActive);
    }
}
