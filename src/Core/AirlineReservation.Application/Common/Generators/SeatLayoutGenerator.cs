using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.Common.Generators;

public enum AircraftLayoutType
{
    NarrowBody, // 2-2 Business (A C | D F), 3-3 Economy (A B C | D E F)
    WideBody    // 2-2 Business (A C | D F), 3-4-3 Economy (A B C | D E F G | H J K - no I)
}

public sealed record SeatDescriptor(
    string SeatNumber,
    int RowNumber,
    string ColumnLetter,
    SeatClass SeatClass,
    decimal BasePriceMultiplier,
    bool IsWindow,
    bool IsAisle,
    bool IsExitRow
);

public static class SeatLayoutGenerator
{
    public static IReadOnlyList<SeatDescriptor> Generate(
        int totalRows,
        AircraftLayoutType layoutType,
        int businessRows = 4,
        IReadOnlyCollection<int>? exitRows = null)
    {
        if (totalRows <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalRows), "Total rows must be greater than zero.");

        if (businessRows < 0 || businessRows > totalRows)
            throw new ArgumentOutOfRangeException(nameof(businessRows), "Business rows must be between 0 and total rows.");

        var exitRowSet = exitRows != null
            ? exitRows.ToHashSet()
            : (layoutType == AircraftLayoutType.NarrowBody ? new HashSet<int> { 12, 13 } : new HashSet<int> { 16, 26 });

        var seats = new List<SeatDescriptor>();

        for (int row = 1; row <= totalRows; row++)
        {
            bool isBusiness = row <= businessRows;
            bool isExitRow = !isBusiness && exitRowSet.Contains(row);

            string[] columns;
            if (isBusiness)
            {
                // Business: 2-2 layout (A C | D F)
                columns = ["A", "C", "D", "F"];
            }
            else if (layoutType == AircraftLayoutType.NarrowBody)
            {
                // Narrow-body Economy: 3-3 layout (A B C | D E F)
                columns = ["A", "B", "C", "D", "E", "F"];
            }
            else
            {
                // Wide-body 777-300ER Economy: 3-4-3 layout (A B C | D E F G | H J K - no I)
                columns = ["A", "B", "C", "D", "E", "F", "G", "H", "J", "K"];
            }

            foreach (var col in columns)
            {
                bool isWindow = isBusiness
                    ? (col is "A" or "F")
                    : (layoutType == AircraftLayoutType.NarrowBody ? col is "A" or "F" : col is "A" or "K");

                bool isAisle = isBusiness
                    ? (col is "C" or "D")
                    : (layoutType == AircraftLayoutType.NarrowBody ? col is "C" or "D" : col is "C" or "D" or "G" or "H");

                decimal multiplier = isBusiness ? 2.5m : (isExitRow ? 1.25m : 1.0m);
                var seatClass = isBusiness ? SeatClass.Business : SeatClass.Economy;

                seats.Add(new SeatDescriptor(
                    SeatNumber: $"{row}{col}",
                    RowNumber: row,
                    ColumnLetter: col,
                    SeatClass: seatClass,
                    BasePriceMultiplier: multiplier,
                    IsWindow: isWindow,
                    IsAisle: isAisle,
                    IsExitRow: isExitRow
                ));
            }
        }

        return seats;
    }
}
