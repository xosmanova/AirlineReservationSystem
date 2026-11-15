namespace AirlineReservation.Application.Common.Exceptions;

/// <summary>
/// Thrown by the persistence layer when a database unique constraint or unique index is violated
/// (e.g. concurrent seat hold collision or duplicate ticket insertion).
/// Application CQRS handlers catch this and translate it into domain-specific ConflictExceptions.
/// </summary>
public class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException()
        : base("A unique constraint violation occurred in the database.")
    {
    }

    public UniqueConstraintViolationException(string message)
        : base(message)
    {
    }

    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
