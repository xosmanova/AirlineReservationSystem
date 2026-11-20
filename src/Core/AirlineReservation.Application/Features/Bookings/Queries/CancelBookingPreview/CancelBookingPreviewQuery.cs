using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.CancelBookingPreview;

/// <summary>
/// Query to calculate and preview the cancellation refund amount and penalties for a booking.
/// </summary>
public record CancelBookingPreviewQuery(int BookingId, Guid UserId, bool IsAdmin) : IRequest<ApiResponse<CancelBookingPreviewDto>>;
