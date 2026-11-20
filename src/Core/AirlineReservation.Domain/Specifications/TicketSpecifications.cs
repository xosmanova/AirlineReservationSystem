using System.Linq.Expressions;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Domain.Specifications;

/// <summary>
/// Centralized specifications and expressions for Ticket domain logic.
/// Single source of truth for active ticket conditions across search queries, seat map availability, and booking rules.
/// </summary>
public static class TicketSpecifications
{
    /// <summary>
    /// Expression defining an active ticket:
    /// Not deleted, not cancelled, and associated with an active booking (not deleted, not cancelled, and not expired).
    /// </summary>
    public static readonly Expression<Func<Ticket, bool>> IsActive = t =>
        !t.IsDeleted &&
        !t.IsCancelled &&
        t.Booking != null &&
        !t.Booking.IsDeleted &&
        t.Booking.Status != BookingStatus.Cancelled &&
        t.Booking.Status != BookingStatus.Expired;

    /// <summary>
    /// Pre-compiled delegate for high-performance in-memory filtering (e.g. seat maps).
    /// </summary>
    public static readonly Func<Ticket, bool> IsActiveCompiled = IsActive.Compile();
}
