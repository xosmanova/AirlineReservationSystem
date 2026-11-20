using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AirlineReservation.Infrastructure.Identity.Services;

/// <summary>
/// Issues HMAC-SHA256 signed JWT access tokens synchronized with the injected TimeProvider.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<JwtSettings> jwtOptions, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);
        _jwtSettings = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public GeneratedTokenResult GenerateToken(TokenUserInfo user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(_jwtSettings.Key) || Encoding.UTF8.GetByteCount(_jwtSettings.Key) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key (Jwt:Key) is missing or shorter than 32 bytes. Configure it via user-secrets or environment variables.");
        }

        if (string.IsNullOrWhiteSpace(_jwtSettings.Issuer) || string.IsNullOrWhiteSpace(_jwtSettings.Audience))
        {
            throw new InvalidOperationException("JWT Issuer and Audience configuration values are required.");
        }

        if (_jwtSettings.ExpiryMinutes <= 0)
        {
            throw new InvalidOperationException("JWT ExpiryMinutes must be greater than zero.");
        }

        // Align timestamps to whole Unix seconds so nbf/exp match JWT numeric date precision exactly
        long nowUnixSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var utcNow = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).UtcDateTime;
        var expiresAtUtc = utcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName));
        }

        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName));
        }

        foreach (var role in user.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim("role", role));
            }
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new GeneratedTokenResult(accessToken, expiresAtUtc);
    }
}
