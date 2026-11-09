using AirlineReservation.Domain.Common;

namespace AirlineReservation.Application.Contracts.Persistence;

/// <summary>
/// Unit of Work contract coordinating operations across multiple repositories
/// and maintaining transactional integrity.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IFlightRepository Flights { get; }
    IBookingRepository Bookings { get; }
    ISeatRepository Seats { get; }
    IAirportRepository Airports { get; }

    IGenericRepository<T> Repository<T>() where T : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
