using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetBookingQuote;

/// <summary>
/// Calculates an itemized server-side seat price quote using IPricingService.
/// </summary>
/// <remarks>
/// The returned quote is informational only and does not hold seats; checkout recalculates prices server-side.
/// </remarks>
public sealed class GetBookingQuoteQueryHandler : IRequestHandler<GetBookingQuoteQuery, ApiResponse<BookingQuoteDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPricingService _pricingService;
    private readonly TimeProvider _timeProvider;

    public GetBookingQuoteQueryHandler(
        IUnitOfWork unitOfWork,
        IPricingService pricingService,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ApiResponse<BookingQuoteDto>> Handle(
        GetBookingQuoteQuery request,
        CancellationToken cancellationToken)
    {
        var seatMapData = await _unitOfWork.Flights.GetFlightSeatMapDataAsync(request.FlightId, cancellationToken);
        if (seatMapData == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
        }

        var aircraftSeatsById = seatMapData.Seats.ToDictionary(s => s.Id);
        var missingFromAircraft = request.SeatIds
            .Where(id => !aircraftSeatsById.ContainsKey(id))
            .ToList();

        if (missingFromAircraft.Count > 0)
        {
            var seatsOnOtherAircraft = await _unitOfWork.Seats.FindAsync(
                s => missingFromAircraft.Contains(s.Id),
                cancellationToken);

            var foundElsewhereIds = seatsOnOtherAircraft.Select(s => s.Id).ToHashSet();
            var unknownSeatIds = missingFromAircraft
                .Where(id => !foundElsewhereIds.Contains(id))
                .ToList();

            if (unknownSeatIds.Count > 0)
            {
                throw new NotFoundException($"Seat(s) not found: {string.Join(", ", unknownSeatIds)}.");
            }

            var mismatchedLabels = seatsOnOtherAircraft.Select(s => $"{s.SeatNumber} (Id: {s.Id})");
            throw new BadRequestException(
                $"Seat(s) {string.Join(", ", mismatchedLabels)} do not belong to flight {seatMapData.FlightNumber}'s aircraft.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var departureUtc = DateTime.SpecifyKind(seatMapData.DepartureTimeUtc, DateTimeKind.Utc);
        bool isSellable = seatMapData.Status is FlightStatus.Scheduled or FlightStatus.Delayed
                          && departureUtc > utcNow;

        if (!isSellable)
        {
            throw new ConflictException(
                $"Flight {seatMapData.FlightNumber} is not sellable (status: {seatMapData.Status}).");
        }

        var requestedSeats = request.SeatIds
            .Select(id => aircraftSeatsById[id])
            .ToList();

        var occupiedSeatNumbers = requestedSeats
            .Where(s => seatMapData.OccupiedSeatIds.Contains(s.Id))
            .Select(s => s.SeatNumber)
            .ToList();

        if (occupiedSeatNumbers.Count > 0)
        {
            throw new ConflictException(
                $"The following seat(s) are already occupied: {string.Join(", ", occupiedSeatNumbers)}.");
        }

        var items = requestedSeats
            .Select(seat =>
            {
                var breakdown = _pricingService.CalculateSeatPrice(new SeatPricingInput(
                    seatMapData.BasePrice,
                    seat.BasePriceMultiplier,
                    seat.IsWindow,
                    seat.IsAisle,
                    seatMapData.DepartureTimeUtc));

                return new BookingQuoteItemDto
                {
                    SeatId = seat.Id,
                    SeatNumber = seat.SeatNumber,
                    SeatClass = seat.SeatClass,
                    IsWindow = seat.IsWindow,
                    IsAisle = seat.IsAisle,
                    IsExitRow = seat.IsExitRow,
                    BaseSeatPrice = breakdown.BaseSeatPrice,
                    PositionFee = breakdown.PositionFee,
                    DemandMultiplier = breakdown.DemandMultiplier,
                    IsLastMinuteDemandApplied = breakdown.IsLastMinuteDemandApplied,
                    FinalPrice = breakdown.FinalPrice
                };
            })
            .ToList();

        decimal totalPrice = items.Sum(i => i.FinalPrice);

        var quote = new BookingQuoteDto
        {
            FlightId = seatMapData.FlightId,
            FlightNumber = seatMapData.FlightNumber,
            DepartureTimeUtc = seatMapData.DepartureTimeUtc,
            FlightBasePrice = seatMapData.BasePrice,
            TotalPrice = totalPrice,
            Items = items
        };

        return ApiResponse<BookingQuoteDto>.SuccessResult(quote, "Booking quote calculated successfully.");
    }
}
