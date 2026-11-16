using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Commands.CancelBooking;

/// <summary>
/// Command to cancel a booking, refund eligible payments according to policy, and release seats.
/// </summary>
public record CancelBookingCommand(int BookingId, Guid UserId, bool IsAdmin) : IRequest<ApiResponse<BookingDetailsDto>>;
