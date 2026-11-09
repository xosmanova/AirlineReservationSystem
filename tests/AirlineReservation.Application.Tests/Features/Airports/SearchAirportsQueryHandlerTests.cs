using System.Linq.Expressions;
using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.Features.Airports.Queries.SearchAirports;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Airports;

public class SearchAirportsQueryHandlerTests
{
    private readonly AirlineMappingProfile _mapper = new();

    [Fact]
    public async Task Handle_ReturnsMappedAirports_WithSuccessResponse()
    {
        // Arrange
        var airport = new Airport
        {
            Id = 1,
            IataCode = "GYD",
            Name = "Heydar Aliyev International Airport",
            City = "Baku",
            Country = "Azerbaijan",
            TimeZone = "Asia/Baku",
            Aliases = "Baku,Bakı,Baki,Баку"
        };

        var fakeAirportRepo = new FakeAirportRepository([airport]);
        var fakeUow = new FakeUnitOfWork(fakeAirportRepo);
        var handler = new SearchAirportsQueryHandler(fakeUow, _mapper);

        // Act
        var result = await handler.Handle(new SearchAirportsQuery("Bakı", 10), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);

        var dto = result.Data[0];
        Assert.Equal(1, dto.Id);
        Assert.Equal("GYD", dto.IataCode);
        Assert.Equal("Heydar Aliyev International Airport", dto.Name);
        Assert.Equal("Baku", dto.City);
        Assert.Equal("Azerbaijan", dto.Country);
        Assert.Equal("Asia/Baku", dto.Timezone);
        Assert.Equal("Baku,Bakı,Baki,Баку", dto.Aliases);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoAirportsMatch()
    {
        // Arrange
        var fakeAirportRepo = new FakeAirportRepository([]);
        var fakeUow = new FakeUnitOfWork(fakeAirportRepo);
        var handler = new SearchAirportsQueryHandler(fakeUow, _mapper);

        // Act
        var result = await handler.Handle(new SearchAirportsQuery("NonExistent", 10), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Equal("No airports found matching the specified search criteria.", result.Message);
    }

    #region Fakes

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public FakeUnitOfWork(IAirportRepository airportRepo)
        {
            Airports = airportRepo;
            Flights = null!;
            Bookings = null!;
            Seats = null!;
            Airlines = null!;
        }

        public IFlightRepository Flights { get; }
        public IBookingRepository Bookings { get; }
        public ISeatRepository Seats { get; }
        public IAirportRepository Airports { get; }
        public IAirlineRepository Airlines { get; }

        public IGenericRepository<T> Repository<T>() where T : BaseEntity => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAirportRepository : IAirportRepository
    {
        private readonly IReadOnlyList<Airport> _airports;

        public FakeAirportRepository(IReadOnlyList<Airport> airports)
        {
            _airports = airports;
        }

        public Task<IReadOnlyList<Airport>> SearchAsync(string? searchTerm, int limit = 10, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_airports);
        }

        public Task<Airport?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Airport>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Airport>> FindAsync(Expression<Func<Airport, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Airport?> FirstOrDefaultAsync(Expression<Func<Airport, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Airport, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<Airport, bool>>? predicate = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Airport> AddAsync(Airport entity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Airport> entities, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public void Update(Airport entity) => throw new NotImplementedException();
        public void Delete(Airport entity) => throw new NotImplementedException();
        public void DeleteRange(IEnumerable<Airport> entities) => throw new NotImplementedException();
    }

    #endregion
}
