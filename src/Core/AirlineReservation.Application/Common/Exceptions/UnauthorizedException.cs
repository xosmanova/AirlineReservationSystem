namespace AirlineReservation.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when authentication fails or credentials are invalid.
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
