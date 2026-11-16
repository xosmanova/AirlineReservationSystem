using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.DTOs.Booking;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetBookingByPnr;

/// <summary>
/// Anonymous query to retrieve confirmed booking details using a 6-character PNR and passenger's last name.
/// </summary>
public record GetBookingByPnrQuery(string PnrCode, string LastName) : IRequest<ApiResponse<BookingDetailsDto>>;
