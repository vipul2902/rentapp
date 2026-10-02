using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentApp.Api.Configuration;
using RentApp.Api.Errors;
using RentApp.Application.Auth;

namespace RentApp.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Creates a new organization with the caller as its owner, and signs them in.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Me), null, response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        auth.LoginAsync(request, cancellationToken);

    /// <summary>Exchanges a refresh token for a new access token and a new (rotated) refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public Task<AuthResponse> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        auth.RefreshAsync(request, cancellationToken);

    /// <summary>Revokes the session the refresh token belongs to. Works even if the access token has expired.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes the signed-in account (needs the password). For the owner this closes the whole
    /// organization. Personal details are erased; financial records are kept but no longer reachable.
    /// </summary>
    [HttpPost("delete-account")]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await auth.DeleteAccountAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    public Task<UserProfile> Me(CancellationToken cancellationToken) => auth.GetCurrentProfileAsync(cancellationToken);
}
