using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Organizations;
using RentApp.Domain.Properties;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("properties", t =>
            t.HasCheckConstraint("ck_properties_status", "status IN ('Active', 'Archived')"));

        builder.Property(p => p.Name).HasMaxLength(Property.NameMaxLength).IsRequired();
        builder.Property(p => p.Address).HasMaxLength(Property.AddressMaxLength).IsRequired();
        builder.Property(p => p.City).HasMaxLength(Property.CityMaxLength).IsRequired();
        builder.Property(p => p.State).HasMaxLength(Property.StateMaxLength);
        builder.Property(p => p.PostalCode).HasMaxLength(Property.PostalCodeMaxLength);
        builder.Property(p => p.ContactPhone).HasMaxLength(Property.PhoneMaxLength);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        // (id, organization_id) is the target of rooms' composite foreign key, which makes it impossible
        // at the database level for a room to belong to a different organization than its property.
        builder.HasAlternateKey(p => new { p.Id, p.OrganizationId });
        builder.HasIndex(p => new { p.OrganizationId, p.Status });

        builder.HasOne<Organization>().WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(p => p.IsArchived);
    }
}

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms", t =>
        {
            t.HasCheckConstraint("ck_rooms_status", "status IN ('Active', 'Unavailable', 'Archived')");
            t.HasCheckConstraint("ck_rooms_capacity", $"capacity BETWEEN {Room.MinCapacity} AND {Room.MaxCapacity}");
        });

        builder.Property(r => r.RoomNumber).HasMaxLength(Room.NumberMaxLength).IsRequired();
        builder.Property(r => r.RoomType).HasMaxLength(Room.TypeMaxLength);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasAlternateKey(r => new { r.Id, r.OrganizationId });

        // Room numbers are unique within a property among rooms that are not archived.
        builder.HasIndex(r => new { r.PropertyId, r.RoomNumber })
            .IsUnique()
            .HasFilter("status <> 'Archived'");
        builder.HasIndex(r => r.OrganizationId);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(r => new { r.PropertyId, r.OrganizationId })
            .HasPrincipalKey(p => new { p.Id, p.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(r => r.IsArchived);
    }
}

internal sealed class BedConfiguration : IEntityTypeConfiguration<Bed>
{
    public void Configure(EntityTypeBuilder<Bed> builder)
    {
        builder.ToTable("beds", t =>
        {
            t.HasCheckConstraint("ck_beds_status", "status IN ('Available', 'Reserved', 'Unavailable', 'Archived')");
            t.HasCheckConstraint("ck_beds_default_rent_positive", "default_monthly_rent IS NULL OR default_monthly_rent > 0");
        });

        builder.Property(b => b.Label).HasMaxLength(Bed.LabelMaxLength).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

        // Labels are unique within a room among beds that are not archived.
        builder.HasIndex(b => new { b.RoomId, b.Label })
            .IsUnique()
            .HasFilter("status <> 'Archived'");
        builder.HasIndex(b => b.OrganizationId);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(b => new { b.RoomId, b.OrganizationId })
            .HasPrincipalKey(r => new { r.Id, r.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(b => b.IsArchived);
    }
}
