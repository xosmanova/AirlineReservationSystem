using AirlineReservation.Application.Common.Generators;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Enums;
using Xunit;

namespace AirlineReservation.Application.Tests.Generators;

public class SvgSeatMapGeneratorTests
{
    private static IReadOnlyList<SeatRowDto> CreateNarrowBodySampleRows()
    {
        var rows = new List<SeatRowDto>();

        // Row 1: Business (2-2)
        rows.Add(new SeatRowDto
        {
            RowNumber = 1,
            SeatClass = SeatClass.Business,
            IsExitRow = false,
            Seats =
            [
                new() { Id = 1, SeatNumber = "1A", RowNumber = 1, ColumnLetter = "A", SeatClass = SeatClass.Business, Price = 360m, IsOccupied = false },
                new() { Id = 2, SeatNumber = "1C", RowNumber = 1, ColumnLetter = "C", SeatClass = SeatClass.Business, Price = 360m, IsOccupied = true },
                new() { Id = 3, SeatNumber = "1D", RowNumber = 1, ColumnLetter = "D", SeatClass = SeatClass.Business, Price = 360m, IsOccupied = false },
                new() { Id = 4, SeatNumber = "1F", RowNumber = 1, ColumnLetter = "F", SeatClass = SeatClass.Business, Price = 360m, IsOccupied = false }
            ]
        });

        // Row 12: Exit Row Economy (3-3)
        rows.Add(new SeatRowDto
        {
            RowNumber = 12,
            SeatClass = SeatClass.Economy,
            IsExitRow = true,
            Seats =
            [
                new() { Id = 10, SeatNumber = "12A", RowNumber = 12, ColumnLetter = "A", SeatClass = SeatClass.Economy, Price = 180m, IsOccupied = false, IsExitRow = true },
                new() { Id = 11, SeatNumber = "12B", RowNumber = 12, ColumnLetter = "B", SeatClass = SeatClass.Economy, Price = 180m, IsOccupied = false, IsExitRow = true },
                new() { Id = 12, SeatNumber = "12C", RowNumber = 12, ColumnLetter = "C", SeatClass = SeatClass.Economy, Price = 180m, IsOccupied = true, IsExitRow = true },
                new() { Id = 13, SeatNumber = "12D", RowNumber = 12, ColumnLetter = "D", SeatClass = SeatClass.Economy, Price = 180m, IsOccupied = false, IsExitRow = true },
                new() { Id = 14, SeatNumber = "12E", RowNumber = 12, ColumnLetter = "E", SeatClass = SeatClass.Economy, Price = 180m, IsOccupied = false, IsExitRow = true },
                new() { Id = 15, SeatNumber = "12F", RowNumber = 12, ColumnLetter = "F", SeatClass = SeatClass.Economy, Price = 180m, IsOccupied = false, IsExitRow = true }
            ]
        });

        return rows;
    }

    private static IReadOnlyList<SeatRowDto> CreateWideBodySampleRows()
    {
        var rows = new List<SeatRowDto>();

        // Row 5: Wide-body Economy (3-4-3: A B C | D E F G | H J K - no I)
        rows.Add(new SeatRowDto
        {
            RowNumber = 5,
            SeatClass = SeatClass.Economy,
            IsExitRow = false,
            Seats =
            [
                new() { Id = 20, SeatNumber = "5A", RowNumber = 5, ColumnLetter = "A", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 21, SeatNumber = "5B", RowNumber = 5, ColumnLetter = "B", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 22, SeatNumber = "5C", RowNumber = 5, ColumnLetter = "C", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 23, SeatNumber = "5D", RowNumber = 5, ColumnLetter = "D", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 24, SeatNumber = "5E", RowNumber = 5, ColumnLetter = "E", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 25, SeatNumber = "5F", RowNumber = 5, ColumnLetter = "F", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 26, SeatNumber = "5G", RowNumber = 5, ColumnLetter = "G", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 27, SeatNumber = "5H", RowNumber = 5, ColumnLetter = "H", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 28, SeatNumber = "5J", RowNumber = 5, ColumnLetter = "J", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false },
                new() { Id = 29, SeatNumber = "5K", RowNumber = 5, ColumnLetter = "K", SeatClass = SeatClass.Economy, Price = 250m, IsOccupied = false }
            ]
        });

        return rows;
    }

    [Fact]
    public void Generate_NarrowBody_ProducesValidSvgMarkup()
    {
        // Arrange
        var rows = CreateNarrowBodySampleRows();

        // Act
        string svg = SvgSeatMapGenerator.Generate("J2101", "Azerbaijan Airlines (AZAL)", "Airbus A320-200", rows);

        // Assert
        Assert.NotNull(svg);
        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>\r\n", svg.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
        Assert.Contains("J2101", svg);
        Assert.Contains("Airbus A320-200", svg);
        Assert.Contains("Azerbaijan Airlines (AZAL)", svg);
        Assert.Contains("data-seat-number=\"1A\"", svg);
        Assert.Contains("data-seat-number=\"12F\"", svg);
    }

    [Fact]
    public void Generate_WideBody777_ContainsAll10ColumnsAndExcludesLetterI()
    {
        // Arrange
        var rows = CreateWideBodySampleRows();

        // Act
        string svg = SvgSeatMapGenerator.Generate("EK301", "Emirates", "Boeing 777-300ER", rows);

        // Assert
        Assert.Contains("data-seat-number=\"5A\"", svg);
        Assert.Contains("data-seat-number=\"5B\"", svg);
        Assert.Contains("data-seat-number=\"5C\"", svg);
        Assert.Contains("data-seat-number=\"5D\"", svg);
        Assert.Contains("data-seat-number=\"5E\"", svg);
        Assert.Contains("data-seat-number=\"5F\"", svg);
        Assert.Contains("data-seat-number=\"5G\"", svg);
        Assert.Contains("data-seat-number=\"5H\"", svg);
        Assert.Contains("data-seat-number=\"5J\"", svg);
        Assert.Contains("data-seat-number=\"5K\"", svg);
        Assert.DoesNotContain("data-seat-number=\"5I\"", svg);
    }

    [Fact]
    public void Generate_WhenSeatIsOccupied_SetsOccupiedClassAndAriaDisabled()
    {
        // Arrange
        var rows = CreateNarrowBodySampleRows();

        // Act
        string svg = SvgSeatMapGenerator.Generate("J2101", "AZAL", "A320", rows);

        // Assert: 1C and 12C are occupied
        Assert.Contains("id=\"seat-2\"", svg);
        Assert.Contains("data-seat-number=\"1C\"", svg);
        Assert.Contains("class=\"seat business occupied\"", svg);
        Assert.Contains("aria-disabled=\"true\"", svg);
    }

    [Fact]
    public void Generate_WhenSeatIsAvailable_SetsAccessibleButtonRole()
    {
        // Arrange
        var rows = CreateNarrowBodySampleRows();

        // Act
        string svg = SvgSeatMapGenerator.Generate("J2101", "AZAL", "A320", rows);

        // Assert: 1A is available business
        Assert.Contains("role=\"button\" tabindex=\"0\"", svg);
        Assert.Contains("data-status=\"available\"", svg);
    }

    [Fact]
    public void Generate_WhenExitRowPresent_RendersExitMarkersAndWings()
    {
        // Arrange
        var rows = CreateNarrowBodySampleRows();

        // Act
        string svg = SvgSeatMapGenerator.Generate("J2101", "AZAL", "A320", rows);

        // Assert
        Assert.Contains("class=\"exit-marker\">EXIT</text>", svg);
        Assert.Contains("class=\"wing\"", svg);
    }
}
