namespace AirlineReservation.Application.DTOs.Auth;

/// <summary>
/// Request payload for authenticating an existing account and issuing a JWT token.
/// </summary>
public sealed record LoginRequestDto(
    string Email,
    string Password);
