using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Auth;
using RentApp.Api.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Rent;
using RentApp.Domain.Users;

namespace RentApp.Api.Controllers;

/// <summary>Monthly rent dues. Reading needs ViewTenants; generating and waiving are owner-only.</summary>
[ApiController]
[Route("api/v1/rent")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
public sealed class RentController(RentService rent) : ControllerBase
{
    [HttpGet("charges")]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<PagedResult<RentChargeDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<RentChargeDto>> List([FromQuery] RentChargeQuery query, CancellationToken cancellationToken) =>
        rent.ListAsync(query, cancellationToken);

    [HttpGet("charges/{id:guid}")]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<RentChargeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<RentChargeDetail> Get(Guid id, CancellationToken cancellationToken) => rent.GetAsync(id, cancellationToken);

    /// <summary>Unpaid charges past their due date, most overdue first.</summary>
    [HttpGet("overdue")]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<PagedResult<RentChargeDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<RentChargeDto>> Overdue([FromQuery] PageQuery page, [FromQuery] Guid? propertyId, CancellationToken cancellationToken) =>
        rent.OverdueAsync(page, propertyId, cancellationToken);

    /// <summary>Outstanding, overdue, due today and due this week, as of today.</summary>
    [HttpGet("summary")]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<RentSummary>(StatusCodes.Status200OK)]
    public Task<RentSummary> Summary([FromQuery] Guid? propertyId, CancellationToken cancellationToken) =>
        rent.SummaryAsync(propertyId, cancellationToken);

    /// <summary>Creates any charges that are due but missing (normally done automatically). Safe to call repeatedly.</summary>
    [HttpPost("generate")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<GenerateResult>(StatusCodes.Status200OK)]
    public Task<GenerateResult> Generate(CancellationToken cancellationToken) => rent.GenerateAsync(cancellationToken);

    /// <summary>Waives part or all of a charge's balance, with a reason. Audited; cannot exceed the balance.</summary>
    [HttpPost("charges/{id:guid}/adjustments")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<RentChargeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<RentChargeDetail> Adjust(Guid id, AdjustChargeRequest request, CancellationToken cancellationToken) =>
        rent.AdjustAsync(id, request, cancellationToken);
}
