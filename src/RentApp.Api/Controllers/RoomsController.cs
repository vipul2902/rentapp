using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Auth;
using RentApp.Api.Errors;
using RentApp.Application.Properties;
using RentApp.Domain.Users;

namespace RentApp.Api.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
public sealed class RoomsController(RoomService rooms) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [RequirePermission(StaffPermissions.ViewProperties)]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    public Task<RoomDto> Get(Guid id, CancellationToken cancellationToken) => rooms.GetAsync(id, cancellationToken);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<RoomDto> Update(Guid id, UpdateRoomRequest request, CancellationToken cancellationToken) =>
        rooms.UpdateAsync(id, request, cancellationToken);

    /// <summary>Archives the room and all of its beds.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await rooms.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/beds")]
    [RequirePermission(StaffPermissions.ViewProperties)]
    [ProducesResponseType<IReadOnlyList<BedDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<BedDto>> ListBeds(Guid id, CancellationToken cancellationToken) => rooms.ListBedsAsync(id, cancellationToken);

    /// <summary>Adds a bed. The label defaults to the next free letter.</summary>
    [HttpPost("{id:guid}/beds")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<BedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BedDto>> AddBed(Guid id, CreateBedRequest request, CancellationToken cancellationToken)
    {
        var bed = await rooms.AddBedAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(BedsController.Get), "Beds", new { id = bed.Id }, bed);
    }
}

[ApiController]
[Route("api/v1/beds")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
public sealed class BedsController(RoomService rooms) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [RequirePermission(StaffPermissions.ViewProperties)]
    [ProducesResponseType<BedDto>(StatusCodes.Status200OK)]
    public Task<BedDto> Get(Guid id, CancellationToken cancellationToken) => rooms.GetBedAsync(id, cancellationToken);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<BedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<BedDto> Update(Guid id, UpdateBedRequest request, CancellationToken cancellationToken) =>
        rooms.UpdateBedAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await rooms.ArchiveBedAsync(id, cancellationToken);
        return NoContent();
    }
}
