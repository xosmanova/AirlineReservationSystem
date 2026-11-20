using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Entities;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetBookingByPnr;

/// <summary>
/// Handles anonymous PNR retrieval, authenticating the request via passenger last name matching.
/// </summary>
public class GetBookingByPnrQueryHandler : IRequestHandler<GetBookingByPnrQuery, ApiResponse<BookingDetailsDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetBookingByPnrQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ApiResponse<BookingDetailsDto>> Handle(
        GetBookingByPnrQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedPnr = request.PnrCode.Trim().ToUpperInvariant();
        var normalizedLastName = request.LastName.Trim();

        var booking = await _unitOfWork.Bookings.GetByPnrWithDetailsAsync(normalizedPnr, cancellationToken);
        if (booking == null)
        {
            throw new NotFoundException("No booking found matching the provided PNR code and passenger last name.");
        }

        // Verify that at least one passenger on this booking matches the provided last name
        bool hasMatchingPassenger = booking.Tickets.Any(t =>
            string.Equals(t.Passenger?.LastName?.Trim(), normalizedLastName, StringComparison.OrdinalIgnoreCase));

        if (!hasMatchingPassenger)
        {
            throw new NotFoundException("No booking found matching the provided PNR code and passenger last name.");
        }

        var dto = booking.ToDetailsDto();
        return ApiResponse<BookingDetailsDto>.SuccessResult(dto, "Booking details retrieved successfully.");
    }
}
