using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for airline carriers.
/// </summary>
public class AirlineRepository : GenericRepository<Airline>, IAirlineRepository
{
    public AirlineRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves all active airlines ordered deterministically by Name, then Code, then Id.
    /// </summary>
    public async Task<IReadOnlyList<Airline>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ThenBy(a => a.Code)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }
}
