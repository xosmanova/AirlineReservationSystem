using System.Text.Json;
using System.Threading.RateLimiting;
using AirlineReservation.Application.Common.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace AirlineReservation.API.Extensions;

/// <summary>
/// Configures built-in ASP.NET Core rate limiting exclusively for the login endpoint.
/// </summary>
public static class RateLimitingExtensions
{
    public const string LoginPolicy = "login";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter();

        // Bind configuration via AddOptions<RateLimiterOptions> so test host overrides apply cleanly
        services.AddOptions<RateLimiterOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                int permitLimit = configuration.GetValue<int?>("RateLimiting:Login:PermitLimit") ?? 5;
                int windowSeconds = configuration.GetValue<int?>("RateLimiting:Login:WindowSeconds") ?? 60;

                if (permitLimit <= 0) permitLimit = 5;
                if (windowSeconds <= 0) windowSeconds = 60;

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(LoginPolicy, httpContext =>
                {
                    var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = TimeSpan.FromSeconds(windowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                    }

                    var response = ApiResponse.FailureResult(
                        "Too many login attempts. Please try again later.",
                        statusCode: StatusCodes.Status429TooManyRequests);

                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(response, JsonOptions),
                        cancellationToken);
                };
            });

        return services;
    }
}
