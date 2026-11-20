using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AirlineReservation.API.Extensions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Auth;
using AirlineReservation.Application.Features.Auth.Commands.Login;
using AirlineReservation.Application.Features.Auth.Commands.Register;
using AirlineReservation.Application.Features.Auth.Queries.GetCurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AirlineReservation.API.Controllers;

/// <summary>
/// Endpoints for user registration, JWT login authentication, and profile/role verification.
/// </summary>
public class AuthController : BaseApiController
{
    /// <summary>
    /// Registers a new Customer user account and returns a signed JWT access token.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new RegisterCommand(request.Email, request.Password, request.FirstName, request.LastName),
            cancellationToken);

        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Authenticates user credentials and issues a signed JWT access token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Returns the currently authenticated user's profile and assigned roles.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid authentication token subject claim.");
        }

        var result = await Mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Verifies Admin role access via JWT Bearer authorization.
    /// </summary>
    [HttpGet("admin-check")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public IActionResult AdminCheck()
    {
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
                    ?? User.FindFirstValue(ClaimTypes.Email);

        return Ok(ApiResponse<object>.SuccessResult(
            new { isAdmin = true, email },
            "Admin authorization confirmed."));
    }
}
