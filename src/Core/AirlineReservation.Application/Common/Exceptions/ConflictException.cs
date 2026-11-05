namespace AirlineReservation.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an operation conflicts with the current state of a resource
/// (e.g., seat is already occupied, duplicate PNR code).
/// Maps to HTTP 409 Conflict.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
