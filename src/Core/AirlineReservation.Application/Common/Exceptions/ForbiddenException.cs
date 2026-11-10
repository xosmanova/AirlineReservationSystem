namespace AirlineReservation.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an authenticated user lacks permission to access a resource.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
