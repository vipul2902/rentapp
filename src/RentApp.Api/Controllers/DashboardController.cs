using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Errors;
using RentApp.Application.Dashboard;

namespace RentApp.Api.Controllers;

/// <summary>
/// The home screen in one request. Any signed-in user may call it: occupancy is included for users who can
/// view properties, and money figures for users who can view tenants.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<DashboardDto> Get([FromQuery] Guid? propertyId, CancellationToken cancellationToken) =>
        dashboard.GetAsync(propertyId, cancellationToken);
}
