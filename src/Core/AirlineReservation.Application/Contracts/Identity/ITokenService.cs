namespace AirlineReservation.Application.Contracts.Identity;

/// <summary>
/// User identity projection passed to token generation without coupling Application to Infrastructure Identity entities.
/// </summary>
public sealed record TokenUserInfo(
    Guid UserId,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// Generated JWT access token and its exact UTC expiration timestamp.
/// </summary>
public sealed record GeneratedTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);

/// <summary>
/// Contract for issuing signed JWT access tokens using injected TimeProvider.
/// </summary>
public interface ITokenService
{
    GeneratedTokenResult GenerateToken(TokenUserInfo user);
}
