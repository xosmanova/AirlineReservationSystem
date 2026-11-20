using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using AirlineReservation.Application.Common.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AirlineReservation.API.Extensions;

/// <summary>
/// Registers stateless JWT Bearer authentication without any cookie authentication schemes.
/// </summary>
public static class AuthenticationExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>, TimeProvider>((options, jwtOptions, timeProvider) =>
            {
                var jwt = jwtOptions.Value;

                if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
                {
                    throw new InvalidOperationException(
                        "JWT signing key (Jwt:Key) is missing or shorter than 32 bytes. Configure it via user-secrets or environment variables.");
                }

                // Preserve standard JWT claim names ("sub", "email", "role") without legacy remapping
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role",
                    // Token issuing and token validation MUST share the same TimeProvider source
                    LifetimeValidator = (notBefore, expires, _, _) =>
                    {
                        if (!expires.HasValue)
                        {
                            return false;
                        }

                        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
                        if (notBefore.HasValue && utcNow < notBefore.Value)
                        {
                            return false;
                        }

                        return utcNow < expires.Value;
                    }
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        if (!context.Response.Headers.ContainsKey("WWW-Authenticate"))
                        {
                            context.Response.Headers.WWWAuthenticate = "Bearer";
                        }

                        var message = string.IsNullOrWhiteSpace(context.ErrorDescription)
                            ? "Authentication is required."
                            : context.ErrorDescription;

                        var body = ApiResponse.FailureResult(message, statusCode: StatusCodes.Status401Unauthorized);
                        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        var body = ApiResponse.FailureResult(
                            "You do not have permission to access this resource.",
                            statusCode: StatusCodes.Status403Forbidden);
                        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
