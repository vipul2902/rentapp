using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Auth;
using RentApp.Api.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Reminders;
using RentApp.Domain.Reminders;
using RentApp.Domain.Users;

namespace RentApp.Api.Controllers;

/// <summary>
/// Rent reminders: the app writes the message, a person sends it (copy, share, SMS or WhatsApp click-to-chat).
/// Nothing is sent automatically. Needs SendReminders; the history list also works with ViewTenants.
/// </summary>
[ApiController]
[Route("api/v1/reminders")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
public sealed class RemindersController(ReminderService reminders) : ControllerBase
{
    public const StaffPermissions HistoryAccess = StaffPermissions.SendReminders | StaffPermissions.ViewTenants;

    /// <summary>Dues that need a reminder today (upcoming, due today, 3+ and 7+ days overdue), with ready-to-send messages.</summary>
    [HttpGet("queue")]
    [RequirePermission(StaffPermissions.SendReminders)]
    [ProducesResponseType<ReminderSuggestions>(StatusCodes.Status200OK)]
    public Task<ReminderSuggestions> Queue([FromQuery] ReminderType? type, CancellationToken cancellationToken) =>
        reminders.QueueAsync(type, cancellationToken);

    /// <summary>The suggested message for one due, and its reminder history.</summary>
    [HttpGet("preview")]
    [RequirePermission(StaffPermissions.SendReminders)]
    [ProducesResponseType<ReminderPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<ReminderPreview> Preview([FromQuery] Guid rentChargeId, CancellationToken cancellationToken) =>
        reminders.PreviewAsync(rentChargeId, cancellationToken);

    /// <summary>Records a reminder that was just copied or sent.</summary>
    [HttpPost]
    [RequirePermission(StaffPermissions.SendReminders)]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReminderDto>> Create(CreateReminderRequest request, CancellationToken cancellationToken)
    {
        var reminder = await reminders.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/reminders/{reminder.Id}", reminder);
    }

    /// <summary>Confirms that a copied reminder was sent. Safe to repeat.</summary>
    [HttpPost("{id:guid}/sent")]
    [RequirePermission(StaffPermissions.SendReminders)]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ReminderDto> MarkSent(Guid id, CancellationToken cancellationToken) => reminders.MarkSentAsync(id, cancellationToken);

    /// <summary>Reminder history, newest first.</summary>
    [HttpGet]
    [RequirePermission(HistoryAccess)]
    [ProducesResponseType<PagedResult<ReminderDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ReminderDto>> List([FromQuery] ReminderListQuery query, CancellationToken cancellationToken) =>
        reminders.ListAsync(query, cancellationToken);
}
