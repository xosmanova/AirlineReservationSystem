using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for airport queries, including autocomplete and multi-alias searches.
/// </summary>
public class AirportRepository : GenericRepository<Airport>, IAirportRepository
{
    public AirportRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Searches active airports by IATA code, city name, airport name, or localized alias.
    /// Results are deterministically ordered by City, then Name, then Id.
    /// </summary>
    public async Task<IReadOnlyList<Airport>> SearchAsync(
        string? searchTerm,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 10;
        if (limit > 50) limit = 50;

        IQueryable<Airport> query = _dbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            query = query.Where(a =>
                EF.Functions.Like(a.IataCode, $"%{term}%") ||
                EF.Functions.Like(a.Name, $"%{term}%") ||
                EF.Functions.Like(a.City, $"%{term}%") ||
                (a.Aliases != null && EF.Functions.Like(a.Aliases, $"%{term}%")));
        }

        return await query
            .OrderBy(a => a.City)
            .ThenBy(a => a.Name)
            .ThenBy(a => a.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
