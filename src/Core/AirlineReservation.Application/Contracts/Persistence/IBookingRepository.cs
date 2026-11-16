using AirlineReservation.Domain.Entities;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Booking-specific repository contract for PNR lookups and user itinerary queries.
/// </summary>
public interface IBookingRepository : IGenericRepository<Booking>
{
    Task<Booking?> GetByIdWithDetailsAsync(int id, bool trackChanges = true, CancellationToken cancellationToken = default);

    Task<Booking?> GetByPnrWithDetailsAsync(string pnrCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetBookingsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);

    Task<bool> PnrExistsAsync(string pnrCode, CancellationToken cancellationToken = default);
}
