namespace AirlineReservation.Application.Common.Models;

/// <summary>
/// Configuration options for JWT access token generation and validation.
/// The signing key (Jwt:Key) must be stored in user-secrets or environment variables, never in source code.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public string Key { get; set; } = string.Empty;
}
