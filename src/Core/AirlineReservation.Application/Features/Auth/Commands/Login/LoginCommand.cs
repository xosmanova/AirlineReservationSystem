using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Auth;
using MediatR;

namespace AirlineReservation.Application.Features.Auth.Commands.Login;

/// <summary>
/// CQRS Command for authenticating user credentials and issuing a JWT token.
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<ApiResponse<AuthResponseDto>>;
