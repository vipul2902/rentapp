using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Application.Common.Time;
using RentApp.Application.Rent;
using RentApp.Domain.Reminders;
using RentApp.Domain.Users;

namespace RentApp.Application.Reminders;

// ---- Contracts ------------------------------------------------------------------------------------

public sealed class CreateReminderRequest
{
    [Required(ErrorMessage = "Choose the rent due to remind about.")]
    public Guid? RentChargeId { get; init; }

    [Required(ErrorMessage = "Choose how the reminder was sent.")]
    [EnumDataType(typeof(ReminderChannel), ErrorMessage = "Choose how the reminder was sent.")]
    public ReminderChannel? Channel { get; init; }

    /// <summary>The text actually sent, if the person edited it. Defaults to the suggested message.</summary>
    [StringLength(Reminder.MessageMaxLength, ErrorMessage = "The message can be at most 1000 characters.")]
    public string? Message { get; init; }
}

public sealed class ReminderListQuery : PageQuery
{
    public Guid? TenantId { get; init; }

    public Guid? RentChargeId { get; init; }

    public ReminderStatus? Status { get; init; }
}

public sealed record ReminderDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    Guid RentChargeId,
    DateOnly PeriodStart,
    ReminderType Type,
    ReminderChannel Channel,
    ReminderStatus Status,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? CreatedByName);

/// <summary>A due that needs a reminder now, with the ready-to-send message.</summary>
public sealed record ReminderSuggestion(RentChargeDto Charge, ReminderType Type, string Message, DateTimeOffset? LastRemindedAt);

public sealed record ReminderSuggestions(DateOnly Today, int Upcoming, int DueToday, int Overdue, int LongOverdue, IReadOnlyList<ReminderSuggestion> Items);

/// <summary>The message for one due, whether or not the queue suggests it, with what was sent before.</summary>
public sealed record ReminderPreview(RentChargeDto Charge, ReminderType Type, string Message, IReadOnlyList<ReminderDto> History);

// ---- Service --------------------------------------------------------------------------------------

/// <summary>
/// Rent reminders (spec §6, Phase 8). The app writes the message; a person sends it by copying, sharing, SMS or
/// WhatsApp click-to-chat. Nothing is sent automatically, and there is no unofficial WhatsApp automation.
/// Needs the SendReminders permission; reading history also works with ViewTenants.
/// </summary>
public sealed class ReminderService(
    IAppDbContext db,
    AuditWriter audit,
    ICurrentUser currentUser,
    OrganizationClock calendar,
    RentService rent,
    TimeProvider clock)
{
    public const string CreatedAction = "reminder.created";
    public const string SentAction = "reminder.sent";
    private const int QueueLimit = 200;

    /// <summary>Dues that need a reminder today, most overdue first. Optionally only one stage.</summary>
    public async Task<ReminderSuggestions> QueueAsync(ReminderType? type, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.SendReminders);
        var today = await calendar.TodayAsync(cancellationToken);
        var charges = await rent.OutstandingDueByAsync(today, today.AddDays(ReminderRules.UpcomingWindowDays), null, null, QueueLimit, cancellationToken);

        var ids = charges.Select(c => c.Id).ToList();
        var earlier = (await db.Reminders.AsNoTracking()
                .Where(r => ids.Contains(r.RentChargeId))
                .Select(r => new { r.RentChargeId, r.Type, r.CreatedOn, r.CreatedAt })
                .ToListAsync(cancellationToken))
            .ToLookup(r => r.RentChargeId);

        var suggestions = new List<ReminderSuggestion>();
        foreach (var charge in charges)
        {
            if (ReminderRules.TypeFor(charge.DueDate, today) is not { } stage)
            {
                continue;
            }

            var history = earlier[charge.Id].Select(r => (r.Type, r.CreatedOn)).ToList();
            if (ReminderRules.IsSuggested(stage, charge.DaysOverdue, history, today))
            {
                var last = earlier[charge.Id].Select(r => (DateTimeOffset?)r.CreatedAt).Max();
                suggestions.Add(new ReminderSuggestion(charge, stage, MessageFor(charge, stage), last));
            }
        }

        return new ReminderSuggestions(
            today,
            suggestions.Count(s => s.Type == ReminderType.Upcoming),
            suggestions.Count(s => s.Type == ReminderType.DueToday),
            suggestions.Count(s => s.Type == ReminderType.Overdue),
            suggestions.Count(s => s.Type == ReminderType.LongOverdue),
            [.. suggestions.Where(s => type is null || s.Type == type)]);
    }

    public async Task<ReminderPreview> PreviewAsync(Guid rentChargeId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.SendReminders);
        var today = await calendar.TodayAsync(cancellationToken);
        var (charge, type) = await RemindableAsync(rentChargeId, today, cancellationToken);
        var history = await ListQuery(new ReminderListQuery { RentChargeId = rentChargeId }).Take(20).ToListAsync(cancellationToken);
        return new ReminderPreview(charge, type, MessageFor(charge, type), history);
    }

    /// <summary>Records a reminder the person just copied or sent. Shared/WhatsApp/SMS count as sent; Copy needs "mark as sent".</summary>
    public async Task<ReminderDto> CreateAsync(CreateReminderRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.SendReminders);
        var today = await calendar.TodayAsync(cancellationToken);
        var (charge, type) = await RemindableAsync(request.RentChargeId!.Value, today, cancellationToken);
        var message = string.IsNullOrWhiteSpace(request.Message)
            ? MessageFor(charge, type)
            : request.Message;

        var reminder = Reminder.Create(
            currentUser.OrganizationId, charge.TenantId, charge.Id, type, message, request.Channel!.Value,
            currentUser.UserId == Guid.Empty ? null : currentUser.UserId, today, clock.GetUtcNow());
        db.Reminders.Add(reminder);
        // The message holds the tenant's name, so it stays out of the audit details.
        audit.Record(CreatedAction, nameof(Reminder), reminder.Id, new { rentChargeId = charge.Id, type, channel = reminder.Channel, status = reminder.Status });
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(reminder.Id, cancellationToken);
    }

    public async Task<ReminderDto> MarkSentAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.SendReminders);
        var reminder = await db.Reminders.SingleOrDefaultAsync(r => r.Id == reminderId, cancellationToken) ?? throw NotFound();
        if (reminder.MarkSent(clock.GetUtcNow()))
        {
            audit.Record(SentAction, nameof(Reminder), reminder.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(reminderId, cancellationToken);
    }

    public async Task<PagedResult<ReminderDto>> ListAsync(ReminderListQuery query, CancellationToken cancellationToken)
    {
        currentUser.EnsureAnyPermission(StaffPermissions.SendReminders | StaffPermissions.ViewTenants);
        var reminders = ListQuery(query);
        var total = await reminders.CountAsync(cancellationToken);
        var page = await reminders.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<ReminderDto>(page, query.Page, query.PageSize, total);
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private async Task<ReminderDto> GetAsync(Guid reminderId, CancellationToken cancellationToken) =>
        await ListQuery(new ReminderListQuery(), reminderId).SingleOrDefaultAsync(cancellationToken) ?? throw NotFound();

    /// <summary>The charge must still be owed. Any owed due can be reminded about, even before the queue suggests it.</summary>
    private async Task<(RentChargeDto Charge, ReminderType Type)> RemindableAsync(Guid rentChargeId, DateOnly today, CancellationToken cancellationToken)
    {
        var charge = (await rent.OutstandingDueByAsync(today, DateOnly.MaxValue, rentChargeId, null, 1, cancellationToken)).SingleOrDefault();
        if (charge is null)
        {
            throw await db.RentCharges.AnyAsync(c => c.Id == rentChargeId, cancellationToken)
                ? new ConflictException("NOTHING_TO_REMIND", "This due is already settled, so there is nothing to remind about.")
                : new NotFoundException("RENT_CHARGE_NOT_FOUND", "The requested rent charge was not found.");
        }

        // Dues further ahead than the upcoming window still get the "upcoming" wording.
        return (charge, ReminderRules.TypeFor(charge.DueDate, today) ?? ReminderType.Upcoming);
    }

    private static string MessageFor(RentChargeDto c, ReminderType type) =>
        ReminderRules.Message(type, new ReminderRules.MessageInput(
            c.TenantName, c.Balance, c.PaidAmount > 0, c.PeriodStart, c.DueDate, c.DaysOverdue, c.PropertyName));

    private IQueryable<ReminderDto> ListQuery(ReminderListQuery query, Guid? reminderId = null)
    {
        var reminders = db.Reminders.AsNoTracking();
        if (reminderId is { } id) reminders = reminders.Where(r => r.Id == id);
        if (query.TenantId is { } tenantId) reminders = reminders.Where(r => r.TenantId == tenantId);
        if (query.RentChargeId is { } chargeId) reminders = reminders.Where(r => r.RentChargeId == chargeId);
        if (query.Status is { } status) reminders = reminders.Where(r => r.Status == status);
        return from r in reminders
               join t in db.Tenants on r.TenantId equals t.Id
               join c in db.RentCharges on r.RentChargeId equals c.Id
               from u in db.Users.Where(u => u.Id == r.CreatedByUserId).DefaultIfEmpty()
               orderby r.CreatedAt descending, r.Id descending
               select new ReminderDto(r.Id, r.TenantId, t.FullName, r.RentChargeId, c.PeriodStart, r.Type, r.Channel, r.Status, r.Message,
                   r.CreatedAt, r.SentAt, u == null ? null : u.Name);
    }

    private static NotFoundException NotFound() => new("REMINDER_NOT_FOUND", "The requested reminder was not found.");
}
