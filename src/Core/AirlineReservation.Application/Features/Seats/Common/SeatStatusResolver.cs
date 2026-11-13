using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.Features.Seats.Common;

/// <summary>
/// Result of resolving a seat's status and ownership.
/// </summary>
public sealed record ResolvedSeatStatus(
    int SeatId,
    SeatStatus Status,
    bool HeldByMe);

/// <summary>
/// Unified single source of truth for seat status resolution across full seat map and lightweight status endpoints.
/// Status Precedence: Blocked > Occupied > Held > Available.
/// </summary>
public static class SeatStatusResolver
{
    public static ResolvedSeatStatus Resolve(
        int seatId,
        IReadOnlySet<int> occupiedTicketSeatIds,
        IReadOnlyDictionary<int, SeatHold> activeHoldsBySeatId,
        Guid? currentUserId)
    {
        // 1. Blocked (Future extension hook)

        // 2. Occupied (Active ticket)
        if (occupiedTicketSeatIds.Contains(seatId))
        {
            return new ResolvedSeatStatus(seatId, SeatStatus.Occupied, false);
        }

        // 3. Held (Active seat hold)
        if (activeHoldsBySeatId.TryGetValue(seatId, out var hold))
        {
            bool heldByMe = currentUserId.HasValue && hold.UserId == currentUserId.Value;
            return new ResolvedSeatStatus(seatId, SeatStatus.Held, heldByMe);
        }

        // 4. Available
        return new ResolvedSeatStatus(seatId, SeatStatus.Available, false);
    }
}
