namespace AirlineReservation.Application.Common.Exceptions;

/// <summary>
/// Thrown by the persistence layer when a database unique constraint or unique index is violated
/// (e.g. concurrent seat hold collision, duplicate ticket insertion, or PNR collision).
/// Contains the optional parsed database constraint or index name to distinguish specific violation causes.
/// </summary>
public class UniqueConstraintViolationException : Exception
{
    /// <summary>
    /// The name of the violated unique index or constraint, if extracted from the underlying database error.
    /// </summary>
    public string? IndexName { get; }

    public UniqueConstraintViolationException()
        : base("A unique constraint violation occurred in the database.")
    {
    }

    public UniqueConstraintViolationException(string message, string? indexName = null)
        : base(message)
    {
        IndexName = indexName;
    }

    public UniqueConstraintViolationException(string message, Exception innerException, string? indexName = null)
        : base(message, innerException)
    {
        IndexName = indexName;
    }
}
