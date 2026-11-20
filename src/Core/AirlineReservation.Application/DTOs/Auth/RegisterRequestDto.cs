namespace AirlineReservation.Application.DTOs.Auth;

/// <summary>
/// Request payload for registering a new Customer account.
/// </summary>
public sealed record RegisterRequestDto(
    string Email,
    string Password,
    string FirstName,
    string LastName);
