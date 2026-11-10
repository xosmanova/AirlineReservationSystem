using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Infrastructure.Identity.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AirlineReservation.IntegrationTests.Auth;

public class JwtTokenServiceTests
{
    private sealed class MutableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private static JwtSettings CreateSettings(string signingKey, int expiryMinutes = 60) => new()
    {
        Issuer = "AirlineReservation.API",
        Audience = "AirlineReservation.Client",
        ExpiryMinutes = expiryMinutes,
        Key = signingKey
    };

    [Fact]
    public void GenerateToken_IncludesExpectedClaimsAndTimestamps_RelativeToTimeProvider()
    {
        var anchorNow = new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcDateTime.AddDays(20500), TimeSpan.Zero);
        var clock = new MutableTimeProvider(anchorNow);
        var signingKey = $"{Guid.NewGuid():N}{Guid.NewGuid():N}";
        var service = new JwtTokenService(Options.Create(CreateSettings(signingKey, expiryMinutes: 45)), clock);
        var userId = Guid.NewGuid();

        var result = service.GenerateToken(new TokenUserInfo(
            userId,
            "pilot@airline.test",
            "Leyla",
            "Mammadova",
            ["Customer", "Admin"]));

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(anchorNow.UtcDateTime.AddMinutes(45), result.ExpiresAtUtc);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parsed = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal("AirlineReservation.API", parsed.Issuer);
        Assert.Contains("AirlineReservation.Client", parsed.Audiences);
        Assert.Equal(userId.ToString(), parsed.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("pilot@airline.test", parsed.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        var roles = parsed.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Equal(2, roles.Count);
        Assert.Contains("Customer", roles);
        Assert.Contains("Admin", roles);
        Assert.Equal(anchorNow.UtcDateTime, parsed.ValidFrom);
        Assert.Equal(anchorNow.UtcDateTime.AddMinutes(45), parsed.ValidTo);
    }

    [Fact]
    public void TokenValidation_UsesSameTimeProvider_RejectsWhenClockReachesExpiry()
    {
        var anchorNow = new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcDateTime.AddDays(20500), TimeSpan.Zero);
        var clock = new MutableTimeProvider(anchorNow);
        var signingKey = $"{Guid.NewGuid():N}{Guid.NewGuid():N}";
        var settings = CreateSettings(signingKey, expiryMinutes: 60);
        var service = new JwtTokenService(Options.Create(settings), clock);

        var generated = service.GenerateToken(new TokenUserInfo(
            Guid.NewGuid(),
            "customer@airline.test",
            "Ali",
            "Aliyev",
            ["Customer"]));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                if (!expires.HasValue) return false;
                var utcNow = clock.GetUtcNow().UtcDateTime;
                if (notBefore.HasValue && utcNow < notBefore.Value) return false;
                return utcNow < expires.Value;
            }
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        // Valid at t0 + 59m
        clock.Advance(TimeSpan.FromMinutes(59));
        var principalBeforeExpiry = handler.ValidateToken(generated.AccessToken, validationParameters, out _);
        Assert.True(principalBeforeExpiry.IsInRole("Customer"));

        // Expired at exact t0 + 60m (ClockSkew = Zero)
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Throws<SecurityTokenInvalidLifetimeException>(() =>
            handler.ValidateToken(generated.AccessToken, validationParameters, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short-key-under-32-bytes")]
    public void GenerateToken_WhenSigningKeyMissingOrTooShort_ThrowsInvalidOperationException(string badKey)
    {
        var anchorNow = new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcDateTime.AddDays(20500), TimeSpan.Zero);
        var clock = new MutableTimeProvider(anchorNow);
        var service = new JwtTokenService(Options.Create(CreateSettings(badKey)), clock);

        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateToken(new TokenUserInfo(Guid.NewGuid(), "a@airline.test", "A", "B", ["Customer"])));
    }
}
