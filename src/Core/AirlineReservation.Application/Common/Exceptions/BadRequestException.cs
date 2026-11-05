namespace AirlineReservation.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an incoming request contains invalid or malformed data.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }

    public BadRequestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
