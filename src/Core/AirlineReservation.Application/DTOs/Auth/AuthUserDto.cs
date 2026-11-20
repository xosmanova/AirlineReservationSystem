namespace AirlineReservation.Application.DTOs.Auth;

/// <summary>
/// Public projection of an authenticated user returned by auth endpoints.
/// </summary>
public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles);
