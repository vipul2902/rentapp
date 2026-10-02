using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Identity;
using RentApp.Domain.Organizations;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", t =>
            t.HasCheckConstraint("ck_refresh_tokens_revocation", "(revoked_at IS NULL) = (revoked_reason IS NULL)"));

        builder.Property(t => t.TokenHash).HasMaxLength(RefreshToken.HashLength).IsFixedLength().IsRequired();
        builder.Property(t => t.RevokedReason).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.OrganizationId);

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
