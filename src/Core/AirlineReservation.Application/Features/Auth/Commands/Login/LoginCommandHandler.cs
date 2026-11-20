using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Auth;
using MediatR;

namespace AirlineReservation.Application.Features.Auth.Commands.Login;

/// <summary>
/// Handles credential verification and JWT token issuance.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, ApiResponse<AuthResponseDto>>
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<ApiResponse<AuthResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);

        return ApiResponse<AuthResponseDto>.SuccessResult(result, "Login successful.");
    }
}
