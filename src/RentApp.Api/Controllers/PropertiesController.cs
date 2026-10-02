using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Auth;
using RentApp.Api.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Properties;
using RentApp.Domain.Users;

namespace RentApp.Api.Controllers;

/// <summary>Properties and their rooms. Reading needs ViewProperties; changes are owner-only.</summary>
[ApiController]
[Route("api/v1/properties")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
public sealed class PropertiesController(PropertyService properties, RoomService rooms) : ControllerBase
{
    [HttpGet]
    [RequirePermission(StaffPermissions.ViewProperties)]
    [ProducesResponseType<PagedResult<PropertyDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<PropertyDto>> List([FromQuery] PropertyListQuery query, CancellationToken cancellationToken) =>
        properties.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequirePermission(StaffPermissions.ViewProperties)]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<PropertyDto> Get(Guid id, CancellationToken cancellationToken) => properties.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PropertyDto>> Create(PropertyRequest request, CancellationToken cancellationToken)
    {
        var property = await properties.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = property.Id }, property);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<PropertyDto> Update(Guid id, PropertyRequest request, CancellationToken cancellationToken) =>
        properties.UpdateAsync(id, request, cancellationToken);

    /// <summary>Archives the property (nothing is deleted). Use POST /restore to bring it back.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await properties.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<PropertyDto> Restore(Guid id, CancellationToken cancellationToken) => properties.RestoreAsync(id, cancellationToken);

    /// <summary>All rooms of the property with their beds (bounded per property, so not paged).</summary>
    [HttpGet("{id:guid}/rooms")]
    [RequirePermission(StaffPermissions.ViewProperties)]
    [ProducesResponseType<IReadOnlyList<RoomDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<RoomDto>> ListRooms(Guid id, [FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        rooms.ListForPropertyAsync(id, includeArchived, cancellationToken);

    /// <summary>Creates a room; by default also creates one bed per capacity slot (A, B, C…).</summary>
    [HttpPost("{id:guid}/rooms")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<RoomDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomDto>> CreateRoom(Guid id, CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var room = await rooms.CreateAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(RoomsController.Get), "Rooms", new { id = room.Id }, room);
    }
}
