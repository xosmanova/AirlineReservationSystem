using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetMyBookings;

/// <summary>
/// Authenticated query retrieving paginated bookings for the requesting user, optionally filtered by status (upcoming, past, cancelled).
/// </summary>
public record GetMyBookingsQuery(
    Guid UserId,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<ApiResponse<PagedResult<BookingDetailsDto>>>;
