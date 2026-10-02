using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentApp.Domain.Reminders;
using RentApp.Domain.Rent;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders", t =>
        {
            t.HasCheckConstraint("ck_reminders_type", "type IN ('Upcoming', 'DueToday', 'Overdue', 'LongOverdue')");
            t.HasCheckConstraint("ck_reminders_channel", "channel IN ('Copy', 'Share', 'WhatsApp', 'Sms')");
            t.HasCheckConstraint("ck_reminders_status", "status IN ('Prepared', 'Sent')");
            t.HasCheckConstraint("ck_reminders_sent_has_time", "(status = 'Sent') = (sent_at IS NOT NULL)");
        });

        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Message).HasMaxLength(Reminder.MessageMaxLength).IsRequired();

        builder.HasIndex(r => new { r.RentChargeId, r.CreatedAt });
        builder.HasIndex(r => new { r.TenantId, r.CreatedAt });
        builder.HasIndex(r => new { r.OrganizationId, r.CreatedAt });

        // The charge and the tenant must both be in the reminder's organization.
        builder.HasOne<RentCharge>()
            .WithMany()
            .HasForeignKey(r => new { r.RentChargeId, r.OrganizationId })
            .HasPrincipalKey(c => new { c.Id, c.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.OrganizationId })
            .HasPrincipalKey(t => new { t.Id, t.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
