using AirlineReservation.Application.Common.Generators;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Generators;

public class SeatLayoutGeneratorTests
{
    [Theory]
    [InlineData(25, AircraftLayoutType.NarrowBody, 4, 142)] // Airbus A320-200: 4*4 + 21*6 = 142
    [InlineData(27, AircraftLayoutType.NarrowBody, 4, 154)] // Boeing 737-800: 4*4 + 23*6 = 154
    [InlineData(30, AircraftLayoutType.NarrowBody, 4, 172)] // Airbus A321: 4*4 + 26*6 = 172
    [InlineData(30, AircraftLayoutType.WideBody, 4, 276)]   // Boeing 777-300ER: 4*4 + 26*10 = 276
    public void Generate_CalculatesExactSeatCount_MatchingTotalSeats(
        int totalRows,
        AircraftLayoutType layout,
        int businessRows,
        int expectedTotalSeats)
    {
        // Act
        var seats = SeatLayoutGenerator.Generate(totalRows, layout, businessRows);

        // Assert
        Assert.Equal(expectedTotalSeats, seats.Count);
    }

    [Theory]
    [InlineData(AircraftLayoutType.NarrowBody, 25)]
    [InlineData(AircraftLayoutType.WideBody, 30)]
    public void Generate_HasNoDuplicateRowAndColumn(AircraftLayoutType layout, int rows)
    {
        // Act
        var seats = SeatLayoutGenerator.Generate(rows, layout, businessRows: 4);

        // Assert
        var uniqueKeys = seats.Select(s => (s.RowNumber, s.ColumnLetter)).ToHashSet();
        Assert.Equal(seats.Count, uniqueKeys.Count);

        var uniqueSeatNumbers = seats.Select(s => s.SeatNumber).ToHashSet();
        Assert.Equal(seats.Count, uniqueSeatNumbers.Count);
    }

    [Fact]
    public void Generate_WideBodyEconomy_NeverContainsLetterI()
    {
        // Act (Boeing 777-300ER widebody 3-4-3)
        var seats = SeatLayoutGenerator.Generate(30, AircraftLayoutType.WideBody, businessRows: 4);

        // Assert: Economy columns must be A, B, C, D, E, F, G, H, J, K (never I)
        var economySeats = seats.Where(s => s.RowNumber > 4).ToList();
        Assert.DoesNotContain(economySeats, s => s.ColumnLetter.Equals("I", StringComparison.OrdinalIgnoreCase));

        var economyColumns = economySeats.Select(s => s.ColumnLetter).Distinct().OrderBy(c => c).ToList();
        var expectedColumns = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "J", "K" };
        Assert.Equal(expectedColumns, economyColumns);
    }

    [Fact]
    public void Generate_NarrowBody_WindowAndAisleFlagsAreConsistent()
    {
        // Act
        var seats = SeatLayoutGenerator.Generate(25, AircraftLayoutType.NarrowBody, businessRows: 4);

        foreach (var seat in seats)
        {
            // A seat can never be both window and aisle
            Assert.False(seat.IsWindow && seat.IsAisle, $"Seat {seat.SeatNumber} cannot be both window and aisle.");

            if (seat.SeatClass == SeatClass.Business)
            {
                // Business 2-2: A, F are Window; C, D are Aisle
                if (seat.ColumnLetter is "A" or "F") Assert.True(seat.IsWindow);
                if (seat.ColumnLetter is "C" or "D") Assert.True(seat.IsAisle);
            }
            else
            {
                // Economy 3-3: A, F are Window; C, D are Aisle; B, E are middle
                if (seat.ColumnLetter is "A" or "F") Assert.True(seat.IsWindow);
                if (seat.ColumnLetter is "C" or "D") Assert.True(seat.IsAisle);
                if (seat.ColumnLetter is "B" or "E")
                {
                    Assert.False(seat.IsWindow);
                    Assert.False(seat.IsAisle);
                }
            }
        }
    }

    [Fact]
    public void Generate_WideBody_WindowAndAisleFlagsAreConsistent()
    {
        // Act (Boeing 777-300ER)
        var seats = SeatLayoutGenerator.Generate(30, AircraftLayoutType.WideBody, businessRows: 4);

        var economySeats = seats.Where(s => s.RowNumber > 4).ToList();
        foreach (var seat in economySeats)
        {
            Assert.False(seat.IsWindow && seat.IsAisle, $"Seat {seat.SeatNumber} cannot be both window and aisle.");

            if (seat.ColumnLetter is "A" or "K") Assert.True(seat.IsWindow);
            if (seat.ColumnLetter is "C" or "D" or "G" or "H") Assert.True(seat.IsAisle);
            if (seat.ColumnLetter is "B" or "E" or "F" or "J")
            {
                Assert.False(seat.IsWindow);
                Assert.False(seat.IsAisle);
            }
        }
    }

    [Fact]
    public void Generate_BusinessAndExitRowMultipliers_AreAccurate()
    {
        // Act
        var seats = SeatLayoutGenerator.Generate(25, AircraftLayoutType.NarrowBody, businessRows: 4, exitRows: new[] { 12, 13 });

        var businessSeats = seats.Where(s => s.RowNumber <= 4).ToList();
        var exitRowSeats = seats.Where(s => s.RowNumber is 12 or 13).ToList();
        var standardEconomySeats = seats.Where(s => s.RowNumber > 4 && s.RowNumber != 12 && s.RowNumber != 13).ToList();

        Assert.All(businessSeats, s =>
        {
            Assert.Equal(SeatClass.Business, s.SeatClass);
            Assert.Equal(2.5m, s.BasePriceMultiplier);
            Assert.False(s.IsExitRow);
        });

        Assert.All(exitRowSeats, s =>
        {
            Assert.Equal(SeatClass.Economy, s.SeatClass);
            Assert.Equal(1.25m, s.BasePriceMultiplier);
            Assert.True(s.IsExitRow);
        });

        Assert.All(standardEconomySeats, s =>
        {
            Assert.Equal(SeatClass.Economy, s.SeatClass);
            Assert.Equal(1.0m, s.BasePriceMultiplier);
            Assert.False(s.IsExitRow);
        });
    }

    [Fact]
    public void Generate_InvalidParameters_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SeatLayoutGenerator.Generate(0, AircraftLayoutType.NarrowBody));
        Assert.Throws<ArgumentOutOfRangeException>(() => SeatLayoutGenerator.Generate(-5, AircraftLayoutType.NarrowBody));
        Assert.Throws<ArgumentOutOfRangeException>(() => SeatLayoutGenerator.Generate(25, AircraftLayoutType.NarrowBody, businessRows: 26));
    }
}
