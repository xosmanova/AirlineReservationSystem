using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Domain.Entities;

public class Seat : BaseEntity
{
    public int AircraftId { get; set; }
    public Aircraft Aircraft { get; set; } = null!;

    public string SeatNumber { get; set; } = string.Empty; // e.g. 1A, 12F
    public int RowNumber { get; set; }                     // 1..30
    public string ColumnLetter { get; set; } = string.Empty; // A, B, C, D, E, F

    public SeatClass SeatClass { get; set; } = SeatClass.Economy;
    public decimal BasePriceMultiplier { get; set; } = 1.0m;

    public bool IsWindow { get; set; }
    public bool IsAisle { get; set; }
    public bool IsExitRow { get; set; }

    // Navigation properties
    public ICollection<Ticket> Tickets { get; set; } = [];
}
