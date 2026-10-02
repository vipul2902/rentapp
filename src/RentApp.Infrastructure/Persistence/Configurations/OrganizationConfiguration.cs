using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Organizations;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", t =>
            t.HasCheckConstraint("ck_organizations_status", "status IN ('Active', 'Suspended')"));

        builder.Property(o => o.Name).HasMaxLength(Organization.NameMaxLength).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.TimeZone).HasMaxLength(64).HasDefaultValue(Organization.DefaultTimeZone);

        // Circular with users.organization_id, so nullable and set after both rows exist.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
