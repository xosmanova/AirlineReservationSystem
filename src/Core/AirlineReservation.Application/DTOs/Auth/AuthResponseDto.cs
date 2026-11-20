namespace AirlineReservation.Application.DTOs.Auth;

/// <summary>
/// Authentication payload returned after successful registration or login.
/// </summary>
public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthUserDto User);
