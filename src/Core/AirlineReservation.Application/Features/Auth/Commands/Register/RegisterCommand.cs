using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Auth;
using MediatR;

namespace AirlineReservation.Application.Features.Auth.Commands.Register;

/// <summary>
/// CQRS Command for registering a new Customer user.
/// </summary>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<ApiResponse<AuthResponseDto>>;
