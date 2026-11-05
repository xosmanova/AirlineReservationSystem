namespace AirlineReservation.Application.Common.Models;

/// <summary>
/// Unified API response wrapper ensuring consistent JSON contract across all endpoints.
/// </summary>
/// <typeparam name="T">Type of the payload</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IReadOnlyList<string>? Errors { get; set; }
    public int StatusCode { get; set; }

    public ApiResponse()
    {
    }

    public ApiResponse(bool success, string message, T? data, int statusCode, IReadOnlyList<string>? errors = null)
    {
        Success = success;
        Message = message;
        Data = data;
        StatusCode = statusCode;
        Errors = errors;
    }

    public static ApiResponse<T> SuccessResult(T data, string message = "Operation completed successfully.", int statusCode = 200)
    {
        return new ApiResponse<T>(true, message, data, statusCode);
    }

    public static ApiResponse<T> FailureResult(string message, IReadOnlyList<string>? errors = null, int statusCode = 400)
    {
        return new ApiResponse<T>(false, message, default, statusCode, errors);
    }

    public static ApiResponse<T> FailureResult(IReadOnlyList<string> errors, string message = "Validation errors occurred.", int statusCode = 400)
    {
        return new ApiResponse<T>(false, message, default, statusCode, errors);
    }
}

/// <summary>
/// Non-generic API response wrapper for endpoints without a data payload.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string>? Errors { get; set; }
    public int StatusCode { get; set; }

    public ApiResponse()
    {
    }

    public ApiResponse(bool success, string message, int statusCode, IReadOnlyList<string>? errors = null)
    {
        Success = success;
        Message = message;
        StatusCode = statusCode;
        Errors = errors;
    }

    public static ApiResponse SuccessResult(string message = "Operation completed successfully.", int statusCode = 200)
    {
        return new ApiResponse(true, message, statusCode);
    }

    public static ApiResponse FailureResult(string message, IReadOnlyList<string>? errors = null, int statusCode = 400)
    {
        return new ApiResponse(false, message, statusCode, errors);
    }

    public static ApiResponse FailureResult(IReadOnlyList<string> errors, string message = "Validation errors occurred.", int statusCode = 400)
    {
        return new ApiResponse(false, message, statusCode, errors);
    }
}
