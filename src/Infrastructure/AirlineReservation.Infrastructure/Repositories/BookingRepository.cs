using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Specialized repository implementation for Booking operations.
/// </summary>
public class BookingRepository : GenericRepository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Booking?> GetByPnrWithDetailsAsync(string pnrCode, CancellationToken cancellationToken = default)
    {
        var normalizedPnr = pnrCode.Trim().ToUpper();

        return await _dbSet
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f.Airline)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Seat)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Passenger)
            .FirstOrDefaultAsync(b => b.PnrCode.ToUpper() == normalizedPnr && !b.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetBookingsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f.Airline)
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Flight)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Passenger)
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> PnrExistsAsync(string pnrCode, CancellationToken cancellationToken = default)
    {
        var normalizedPnr = pnrCode.Trim().ToUpper();
        return await _dbSet.AnyAsync(b => b.PnrCode.ToUpper() == normalizedPnr && !b.IsDeleted, cancellationToken);
    }
}
