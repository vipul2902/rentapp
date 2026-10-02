using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Organizations;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_role", "role IN ('Owner', 'Staff')");
            t.HasCheckConstraint("ck_users_status", "status IN ('Active', 'Disabled', 'Deleted')");
            t.HasCheckConstraint("ck_users_permissions_range", $"permissions >= 0 AND permissions <= {(int)StaffPermissions.All}");
            t.HasCheckConstraint("ck_users_owner_has_no_permissions", "role <> 'Owner' OR permissions = 0");
        });

        builder.Property(u => u.Name).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(User.PhoneMaxLength);
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Permissions).HasConversion<int>();

        // One account per email across the whole system: login is by email alone.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
        builder.HasIndex(u => u.OrganizationId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(u => u.IsActive);
        builder.Ignore(u => u.IsOwner);
        builder.Ignore(u => u.EffectivePermissions);
    }
}
