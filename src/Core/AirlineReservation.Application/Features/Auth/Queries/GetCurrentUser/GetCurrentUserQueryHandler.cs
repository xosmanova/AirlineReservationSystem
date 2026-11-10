using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Auth;
using MediatR;

namespace AirlineReservation.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>
/// Retrieves the current authenticated user projection.
/// </summary>
public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, ApiResponse<AuthUserDto>>
{
    private readonly IAuthService _authService;

    public GetCurrentUserQueryHandler(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<ApiResponse<AuthUserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(request.UserId, cancellationToken);
        return ApiResponse<AuthUserDto>.SuccessResult(user, "Current user retrieved successfully.");
    }
}
