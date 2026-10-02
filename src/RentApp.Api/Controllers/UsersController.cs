using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Auth;
using RentApp.Api.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Users;

namespace RentApp.Api.Controllers;

/// <summary>Owner-only staff management. Users from other organizations are reported as not found.</summary>
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize(Policy = Policies.OwnerOnly)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
public sealed class UsersController(UserManagementService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<StaffMember>>(StatusCodes.Status200OK)]
    public Task<PagedResult<StaffMember>> List([FromQuery] UserListQuery query, CancellationToken cancellationToken) =>
        users.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StaffMember>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<StaffMember> Get(Guid id, CancellationToken cancellationToken) => users.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<StaffMember>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffMember>> Create(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var staff = await users.CreateStaffAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = staff.Id }, staff);
    }

    [HttpPut("{id:guid}/permissions")]
    [ProducesResponseType<StaffMember>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<StaffMember> UpdatePermissions(Guid id, UpdatePermissionsRequest request, CancellationToken cancellationToken) =>
        users.UpdatePermissionsAsync(id, request, cancellationToken);

    /// <summary>Disables the account and ends all of its sessions.</summary>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType<StaffMember>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<StaffMember> Disable(Guid id, CancellationToken cancellationToken) => users.DisableAsync(id, cancellationToken);

    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType<StaffMember>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<StaffMember> Enable(Guid id, CancellationToken cancellationToken) => users.EnableAsync(id, cancellationToken);

    /// <summary>Sets a new password for a staff member and ends all of their sessions.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await users.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }
}
