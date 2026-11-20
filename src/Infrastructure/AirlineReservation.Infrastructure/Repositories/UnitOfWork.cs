using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Domain.Common;
using AirlineReservation.Infrastructure.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AirlineReservation.Infrastructure.Repositories;

/// <summary>
/// Unit of work implementation coordinating multiple repositories and transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    private IFlightRepository? _flights;
    private IBookingRepository? _bookings;
    private ISeatRepository? _seats;
    private IAirportRepository? _airports;
    private IAirlineRepository? _airlines;
    private ISeatHoldRepository? _seatHolds;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IFlightRepository Flights => _flights ??= new FlightRepository(_context);
    public IBookingRepository Bookings => _bookings ??= new BookingRepository(_context);
    public ISeatRepository Seats => _seats ??= new SeatRepository(_context);
    public IAirportRepository Airports => _airports ??= new AirportRepository(_context);
    public IAirlineRepository Airlines => _airlines ??= new AirlineRepository(_context);
    public ISeatHoldRepository SeatHolds => _seatHolds ??= new SeatHoldRepository(_context);

    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        return (IGenericRepository<T>)_repositories.GetOrAdd(
            typeof(T),
            _ => new GenericRepository<T>(_context));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                var (isViolation, indexName) = ExtractUniqueConstraintViolation(ex);
                if (isViolation)
                {
                    throw new UniqueConstraintViolationException("A unique constraint or unique index was violated.", ex, indexName);
                }

                if (attempt == 0 && _transaction == null && IsDeadlockException(ex))
                {
                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                throw;
            }
        }

        return 0;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException ex)
        {
            var (isViolation, indexName) = ExtractUniqueConstraintViolation(ex);
            await RollbackTransactionAsync(cancellationToken);

            if (isViolation)
            {
                throw new UniqueConstraintViolationException(
                    "A unique constraint or unique index was violated during transaction commit.",
                    ex,
                    indexName);
            }

            throw;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    private static (bool IsViolation, string? IndexName) ExtractUniqueConstraintViolation(DbUpdateException ex)
    {
        var current = ex.InnerException;
        while (current != null)
        {
            if (current is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                var match = Regex.Match(sqlEx.Message, @"(?:unique index|constraint)\s+'([^']+)'", RegexOptions.IgnoreCase);
                var indexName = match.Success ? match.Groups[1].Value : null;
                return (true, indexName);
            }

            current = current.InnerException;
        }

        return (false, null);
    }

    private static bool IsDeadlockException(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is SqlException sqlEx && sqlEx.Number == 1205)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _transaction?.Dispose();
                _context.Dispose();
            }

            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }

            await _context.DisposeAsync();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
