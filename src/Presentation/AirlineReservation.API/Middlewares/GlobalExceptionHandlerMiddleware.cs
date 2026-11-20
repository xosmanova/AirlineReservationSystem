using System.Net;
using System.Text.Json;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;

namespace AirlineReservation.API.Middlewares;

/// <summary>
/// Global exception handling middleware converting unhandled exceptions
/// into standardized ApiResponse error responses.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate _next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        this._next = _next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            NotFoundException notFoundEx => (
                (int)HttpStatusCode.NotFound,
                ApiResponse.FailureResult(notFoundEx.Message, statusCode: (int)HttpStatusCode.NotFound)
            ),
            UnauthorizedException unauthorizedEx => (
                (int)HttpStatusCode.Unauthorized,
                ApiResponse.FailureResult(unauthorizedEx.Message, statusCode: (int)HttpStatusCode.Unauthorized)
            ),
            ForbiddenException forbiddenEx => (
                (int)HttpStatusCode.Forbidden,
                ApiResponse.FailureResult(forbiddenEx.Message, statusCode: (int)HttpStatusCode.Forbidden)
            ),
            AppValidationException validationEx => (
                (int)HttpStatusCode.BadRequest,
                ApiResponse.FailureResult(
                    validationEx.Errors.Values.SelectMany(errs => errs).ToList(),
                    "Validation errors occurred.",
                    (int)HttpStatusCode.BadRequest)
            ),
            ConflictException conflictEx => (
                (int)HttpStatusCode.Conflict,
                ApiResponse.FailureResult(conflictEx.Message, statusCode: (int)HttpStatusCode.Conflict)
            ),
            BadRequestException badRequestEx => (
                (int)HttpStatusCode.BadRequest,
                ApiResponse.FailureResult(badRequestEx.Message, statusCode: (int)HttpStatusCode.BadRequest)
            ),
            KeyNotFoundException keyEx => (
                (int)HttpStatusCode.NotFound,
                ApiResponse.FailureResult(keyEx.Message, statusCode: (int)HttpStatusCode.NotFound)
            ),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                ApiResponse.FailureResult("An internal server error occurred. Please try again later.", statusCode: (int)HttpStatusCode.InternalServerError)
            )
        };

        context.Response.StatusCode = statusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(response, jsonOptions);
        await context.Response.WriteAsync(json);
    }
}
