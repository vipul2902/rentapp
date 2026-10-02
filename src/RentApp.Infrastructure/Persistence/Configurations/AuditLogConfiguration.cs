using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Audit;
using RentApp.Domain.Organizations;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.Property(a => a.Action).HasMaxLength(AuditLog.ActionMaxLength).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(AuditLog.EntityTypeMaxLength).IsRequired();
        builder.Property(a => a.Details).HasColumnType("jsonb");

        builder.HasIndex(a => new { a.OrganizationId, a.CreatedAt });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });

        builder.HasOne<Organization>().WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
