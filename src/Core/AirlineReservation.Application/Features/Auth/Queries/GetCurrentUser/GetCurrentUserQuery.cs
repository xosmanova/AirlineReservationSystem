using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Auth;
using MediatR;

namespace AirlineReservation.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>
/// CQRS Query for retrieving the authenticated user's profile and roles.
/// </summary>
public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<ApiResponse<AuthUserDto>>;
