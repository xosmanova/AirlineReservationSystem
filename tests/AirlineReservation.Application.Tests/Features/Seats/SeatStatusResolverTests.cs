using AirlineReservation.Application.Features.Seats.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Seats;

public class SeatStatusResolverTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    [Fact]
    public void Resolve_WhenSeatOccupiedByTicket_ShouldReturnOccupiedEvenIfHeld()
    {
        var occupiedSeatIds = new HashSet<int> { 10 };
        var activeHolds = new Dictionary<int, SeatHold>
        {
            [10] = new() { SeatId = 10, UserId = TestUserId }
        };

        // Ticket takes precedence over hold
        var result = SeatStatusResolver.Resolve(10, occupiedSeatIds, activeHolds, TestUserId);

        Assert.Equal(SeatStatus.Occupied, result.Status);
        Assert.False(result.HeldByMe);
    }

    [Fact]
    public void Resolve_WhenSeatHeldByCurrentUser_ShouldReturnHeldWithHeldByMeTrue()
    {
        var occupiedSeatIds = new HashSet<int>();
        var activeHolds = new Dictionary<int, SeatHold>
        {
            [15] = new() { SeatId = 15, UserId = TestUserId }
        };

        var result = SeatStatusResolver.Resolve(15, occupiedSeatIds, activeHolds, TestUserId);

        Assert.Equal(SeatStatus.Held, result.Status);
        Assert.True(result.HeldByMe);
    }

    [Fact]
    public void Resolve_WhenSeatHeldByOtherUser_ShouldReturnHeldWithHeldByMeFalse()
    {
        var occupiedSeatIds = new HashSet<int>();
        var activeHolds = new Dictionary<int, SeatHold>
        {
            [15] = new() { SeatId = 15, UserId = OtherUserId }
        };

        var result = SeatStatusResolver.Resolve(15, occupiedSeatIds, activeHolds, TestUserId);

        Assert.Equal(SeatStatus.Held, result.Status);
        Assert.False(result.HeldByMe);
    }

    [Fact]
    public void Resolve_WhenSeatHeldAndAnonymousCaller_ShouldReturnHeldWithHeldByMeFalse()
    {
        var occupiedSeatIds = new HashSet<int>();
        var activeHolds = new Dictionary<int, SeatHold>
        {
            [15] = new() { SeatId = 15, UserId = TestUserId }
        };

        var result = SeatStatusResolver.Resolve(15, occupiedSeatIds, activeHolds, currentUserId: null);

        Assert.Equal(SeatStatus.Held, result.Status);
        Assert.False(result.HeldByMe);
    }

    [Fact]
    public void Resolve_WhenSeatNeitherOccupiedNorHeld_ShouldReturnAvailable()
    {
        var occupiedSeatIds = new HashSet<int>();
        var activeHolds = new Dictionary<int, SeatHold>();

        var result = SeatStatusResolver.Resolve(20, occupiedSeatIds, activeHolds, TestUserId);

        Assert.Equal(SeatStatus.Available, result.Status);
        Assert.False(result.HeldByMe);
    }
}
