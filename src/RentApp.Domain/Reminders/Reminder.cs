using System.Globalization;
using RentApp.Domain.Common;

namespace RentApp.Domain.Reminders;

/// <summary>Which reminder a due needs, from the spec's stages (§6): upcoming, due today, 3 and 7 days overdue.</summary>
public enum ReminderType
{
    /// <summary>Due within the next 3 days.</summary>
    Upcoming = 1,
    DueToday = 2,

    /// <summary>1–6 days late. The reminder queue suggests it from day 3, after a short grace period.</summary>
    Overdue = 3,

    /// <summary>7 or more days late. Suggested again every 7 days until paid.</summary>
    LongOverdue = 4,
}

/// <summary>How the person sent it. All are started by a person; nothing is sent automatically.</summary>
public enum ReminderChannel
{
    Copy = 1,
    Share = 2,
    WhatsApp = 3,
    Sms = 4,
}

public enum ReminderStatus
{
    /// <summary>Copied, but the person has not yet confirmed it was sent.</summary>
    Prepared = 1,
    Sent = 2,
}

/// <summary>A reminder message prepared or sent to a tenant about one due. Kept as history.</summary>
public sealed class Reminder : Entity, IOrganizationScoped
{
    public const int MessageMaxLength = 1000;

    private Reminder()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid RentChargeId { get; private set; }

    public ReminderType Type { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public ReminderChannel Channel { get; private set; }

    public ReminderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The business date (organization time zone) it was created; drives the weekly repeat rule.</summary>
    public DateOnly CreatedOn { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    /// <summary>
    /// Sharing to an app (share sheet, WhatsApp, SMS) counts as sent: the person chose where it goes.
    /// Copying needs a separate "mark as sent".
    /// </summary>
    public static Reminder Create(
        Guid organizationId, Guid tenantId, Guid rentChargeId, ReminderType type, string message, ReminderChannel channel,
        Guid? createdByUserId, DateOnly createdOn, DateTimeOffset at)
    {
        var sent = channel != ReminderChannel.Copy;
        return new Reminder
        {
            OrganizationId = organizationId,
            TenantId = tenantId,
            RentChargeId = rentChargeId,
            Type = type,
            Message = message.Trim(),
            Channel = channel,
            Status = sent ? ReminderStatus.Sent : ReminderStatus.Prepared,
            SentAt = sent ? at : null,
            CreatedByUserId = createdByUserId,
            CreatedAt = at,
            CreatedOn = createdOn,
        };
    }

    /// <summary>Returns false when it was already marked as sent (repeating is harmless).</summary>
    public bool MarkSent(DateTimeOffset at)
    {
        if (Status == ReminderStatus.Sent)
        {
            return false;
        }

        Status = ReminderStatus.Sent;
        SentAt = at;
        return true;
    }
}

/// <summary>When a due needs a reminder, and what it says. Pure functions, unit-tested.</summary>
public static class ReminderRules
{
    public const int UpcomingWindowDays = 3;
    public const int OverdueGraceDays = 3;
    public const int LongOverdueDays = 7;
    public const int LongOverdueRepeatDays = 7;

    /// <summary>The reminder that fits a due today, or null if it isn't due within the upcoming window.</summary>
    public static ReminderType? TypeFor(DateOnly dueDate, DateOnly today)
    {
        var days = today.DayNumber - dueDate.DayNumber;
        return days switch
        {
            >= LongOverdueDays => ReminderType.LongOverdue,
            >= 1 => ReminderType.Overdue,
            0 => ReminderType.DueToday,
            >= -UpcomingWindowDays => ReminderType.Upcoming,
            _ => null,
        };
    }

    /// <summary>
    /// Whether the queue should suggest a reminder now. Each stage is suggested once per due; 1–2 days late
    /// is a grace period; once 7+ days late it comes back weekly.
    /// </summary>
    public static bool IsSuggested(ReminderType type, int daysOverdue, IReadOnlyCollection<(ReminderType Type, DateOnly On)> earlier, DateOnly today) => type switch
    {
        ReminderType.Overdue when daysOverdue < OverdueGraceDays => false,
        ReminderType.LongOverdue => !earlier.Any(r => r.Type == ReminderType.LongOverdue && today.DayNumber - r.On.DayNumber < LongOverdueRepeatDays),
        _ => !earlier.Any(r => r.Type == type),
    };

    public sealed record MessageInput(
        string TenantName, decimal Balance, bool PartlyPaid, DateOnly PeriodStart, DateOnly DueDate, int DaysOverdue, string PropertyName);

    public static string Message(ReminderType type, MessageInput m)
    {
        var name = FirstName(m.TenantName);
        var month = m.PeriodStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var due = m.DueDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var amount = IndianRupees.Format(m.Balance, alwaysShowPaise: false);
        var what = m.PartlyPaid ? $"the remaining rent of {amount} for {month}" : $"your rent of {amount} for {month}";
        var days = m.DaysOverdue == 1 ? "1 day" : $"{m.DaysOverdue} days";
        var body = type switch
        {
            ReminderType.Upcoming => $"Hi {name}, a friendly reminder that {what} is due on {due}.",
            ReminderType.DueToday => $"Hi {name}, {what} is due today. Please make the payment at your earliest convenience.",
            ReminderType.Overdue => $"Hi {name}, {what} was due on {due} and is now {days} overdue. Please make the payment at your earliest convenience.",
            _ => $"Hi {name}, {what} is {days} overdue (due on {due}). Please make the payment as soon as possible, or let us know if there is a problem.",
        };
        return $"{body} Thank you! – {m.PropertyName}";
    }

    private static string FirstName(string fullName)
    {
        var trimmed = fullName.Trim();
        var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? trimmed[..space] : trimmed;
    }
}
