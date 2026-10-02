using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Auth;
using RentApp.Api.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Api.Controllers;

/// <summary>Tenants and their tenancies. Reading needs ViewTenants; changes are owner-only.</summary>
[ApiController]
[Route("api/v1/tenants")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
public sealed class TenantsController(TenantService tenants) : ControllerBase
{
    [HttpGet]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<PagedResult<TenantSummary>>(StatusCodes.Status200OK)]
    public Task<PagedResult<TenantSummary>> List([FromQuery] TenantListQuery query, CancellationToken cancellationToken) =>
        tenants.ListAsync(query, cancellationToken);

    /// <summary>The tenant with their current tenancy and full history.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<TenantDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<TenantDetail> Get(Guid id, CancellationToken cancellationToken) => tenants.GetAsync(id, cancellationToken);

    /// <summary>Adds a tenant, optionally assigning a bed (moveIn) in the same step.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<TenantDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantDetail>> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenants.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<TenantDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<TenantDetail> Update(Guid id, TenantDetailsRequest request, CancellationToken cancellationToken) =>
        tenants.UpdateAsync(id, request, cancellationToken);

    /// <summary>Archives a tenant who has no bed. History is kept.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await tenants.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/move-in")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<TenantDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<TenantDetail> MoveIn(Guid id, MoveInRequest request, CancellationToken cancellationToken) =>
        tenants.MoveInAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/move-out")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<TenantDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<TenantDetail> MoveOut(Guid id, MoveOutRequest request, CancellationToken cancellationToken) =>
        tenants.MoveOutAsync(id, request, cancellationToken);

    /// <summary>Moves the tenant to another bed (ends the current tenancy, starts a new one).</summary>
    [HttpPost("{id:guid}/move")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<TenantDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<TenantDetail> Move(Guid id, MoveTenantRequest request, CancellationToken cancellationToken) =>
        tenants.MoveAsync(id, request, cancellationToken);

    /// <summary>Changes rent, deposit or due day of the current tenancy (applies to charges not yet generated).</summary>
    [HttpPut("{id:guid}/tenancy")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<TenantDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<TenantDetail> UpdateTerms(Guid id, UpdateTenancyTermsRequest request, CancellationToken cancellationToken) =>
        tenants.UpdateTermsAsync(id, request, cancellationToken);
}
