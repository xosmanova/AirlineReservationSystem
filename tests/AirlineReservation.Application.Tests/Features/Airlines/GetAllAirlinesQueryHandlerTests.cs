using System.Linq.Expressions;
using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.Features.Airlines.Queries.GetAllAirlines;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Airlines;

public class GetAllAirlinesQueryHandlerTests
{
    private readonly AirlineMappingProfile _mapper = new();

    [Fact]
    public async Task Handle_ReturnsAllAirlines_MappedToDto()
    {
        // Arrange
        var airlines = new List<Airline>
        {
            new() { Id = 1, Code = "J2", Name = "Azerbaijan Airlines", LogoUrl = "https://azal.az/logo.png" },
            new() { Id = 2, Code = "TK", Name = "Turkish Airlines", LogoUrl = "https://turkishairlines.com/logo.png" }
        };

        var fakeRepo = new FakeAirlineRepository(airlines);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new GetAllAirlinesQueryHandler(fakeUow, _mapper);

        // Act
        var result = await handler.Handle(new GetAllAirlinesQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("J2", result.Data[0].Code);
        Assert.Equal("Azerbaijan Airlines", result.Data[0].Name);
        Assert.Equal("TK", result.Data[1].Code);
        Assert.Equal("Turkish Airlines", result.Data[1].Name);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoAirlinesExist()
    {
        // Arrange
        var fakeRepo = new FakeAirlineRepository([]);
        var fakeUow = new FakeUnitOfWork(fakeRepo);
        var handler = new GetAllAirlinesQueryHandler(fakeUow, _mapper);

        // Act
        var result = await handler.Handle(new GetAllAirlinesQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Equal("No airlines found.", result.Message);
    }

    #region Fakes

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public FakeUnitOfWork(IAirlineRepository airlineRepo)
        {
            Airlines = airlineRepo;
            Flights = null!;
            Bookings = null!;
            Seats = null!;
            Airports = null!;
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

    private sealed class FakeAirlineRepository : IAirlineRepository
    {
        private readonly IReadOnlyList<Airline> _airlines;

        public FakeAirlineRepository(IReadOnlyList<Airline> airlines)
        {
            _airlines = airlines;
        }

        public Task<IReadOnlyList<Airline>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_airlines);
        }

        public Task<Airline?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Airline>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Airline>> FindAsync(Expression<Func<Airline, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Airline?> FirstOrDefaultAsync(Expression<Func<Airline, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Airline, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<Airline, bool>>? predicate = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Airline> AddAsync(Airline entity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Airline> entities, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public void Update(Airline entity) => throw new NotImplementedException();
        public void Delete(Airline entity) => throw new NotImplementedException();
        public void DeleteRange(IEnumerable<Airline> entities) => throw new NotImplementedException();
    }

    #endregion
}
