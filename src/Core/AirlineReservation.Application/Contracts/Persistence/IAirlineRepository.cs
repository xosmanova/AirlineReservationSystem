using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Repository contract for airline carrier queries.
/// </summary>
public interface IAirlineRepository : IGenericRepository<Airline>
{
    /// <summary>
    /// Retrieves all active airlines ordered deterministically by Name, then Code.
    /// </summary>
    Task<IReadOnlyList<Airline>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
}
