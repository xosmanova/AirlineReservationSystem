using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Commands.Checkout;

/// <summary>
/// CQRS handler executing the end-to-end checkout pipeline within an ACID database transaction.
/// Consumes active seat holds, verifies sellability and seat occupancy, recalculates server-side pricing,
/// charges payment via IPaymentGateway, generates a unique PNR, and creates Booking, Ticket, and Payment records.
/// </summary>
public class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, ApiResponse<CheckoutResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPricingService _pricingService;
    private readonly IPnrGenerator _pnrGenerator;
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _timeProvider;

    public CheckoutCommandHandler(
        IUnitOfWork unitOfWork,
        IPricingService pricingService,
        IPnrGenerator pnrGenerator,
        IPaymentGateway paymentGateway,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        _pnrGenerator = pnrGenerator ?? throw new ArgumentNullException(nameof(pnrGenerator));
        _paymentGateway = paymentGateway ?? throw new ArgumentNullException(nameof(paymentGateway));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ApiResponse<CheckoutResultDto>> Handle(
        CheckoutCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Fetch flight seat map data to validate flight status, schedule, and aircraft seats
        var seatMapData = await _unitOfWork.Flights.GetFlightSeatMapDataAsync(request.FlightId, cancellationToken);
        if (seatMapData == null)
        {
            throw new NotFoundException(nameof(Flight), request.FlightId);
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

        // 2. Validate all requested seat IDs belong to this flight's aircraft
        var aircraftSeatsById = seatMapData.Seats.ToDictionary(s => s.Id);
        var requestedSeatIds = request.Passengers.Select(p => p.SeatId).Distinct().ToList();

        var missingFromAircraft = requestedSeatIds
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

        // 3. Validate none of the requested seats are already occupied by confirmed tickets
        var occupiedSeatNumbers = requestedSeatIds
            .Where(id => seatMapData.OccupiedSeatIds.Contains(id))
            .Select(id => aircraftSeatsById[id].SeatNumber)
            .ToList();

        if (occupiedSeatNumbers.Count > 0)
        {
            throw new ConflictException(
                $"The following seat(s) are already occupied: {string.Join(", ", occupiedSeatNumbers)}.");
        }

        // 4. Validate active seat holds: user must hold an active (non-expired) hold for EVERY requested seat
        var userHolds = await _unitOfWork.SeatHolds.GetUserActiveHoldsForFlightAsync(
            request.FlightId,
            request.UserId,
            utcNow,
            cancellationToken);

        var heldSeatIds = userHolds.Select(h => h.SeatId).ToHashSet();
        var unheldSeatIds = requestedSeatIds
            .Where(id => !heldSeatIds.Contains(id))
            .ToList();

        if (unheldSeatIds.Count > 0)
        {
            var unheldSeatNumbers = unheldSeatIds.Select(id => aircraftSeatsById[id].SeatNumber);
            throw new ConflictException(
                $"Active seat hold required for checkout. You do not hold an active reservation for seat(s): {string.Join(", ", unheldSeatNumbers)}. Seat holds may have expired.");
        }

        // 5. Calculate itemized server-side pricing for each passenger's seat
        var seatPrices = new Dictionary<int, decimal>();
        decimal totalAmount = 0m;

        foreach (var passenger in request.Passengers)
        {
            var seat = aircraftSeatsById[passenger.SeatId];
            var breakdown = _pricingService.CalculateSeatPrice(new SeatPricingInput(
                seatMapData.BasePrice,
                seat.BasePriceMultiplier,
                seat.IsWindow,
                seat.IsAisle,
                seatMapData.DepartureTimeUtc));

            seatPrices[seat.Id] = breakdown.FinalPrice;
            totalAmount += breakdown.FinalPrice;
        }

        // 6. Process payment through payment gateway abstraction
        var paymentRequest = new PaymentProcessRequest(
            totalAmount,
            "AZN",
            request.Card.CardNumber,
            request.Card.ExpiryMonthYear,
            request.Card.Cvv,
            request.Card.CardHolderName);

        var paymentResult = await _paymentGateway.ProcessPaymentAsync(paymentRequest, cancellationToken);
        if (!paymentResult.Success)
        {
            throw new BadRequestException(
                $"Payment processing failed: {paymentResult.ErrorMessage ?? "Payment was declined by issuer."}");
        }

        // 7. Generate a unique 6-character PNR code with collision retry loop
        string pnrCode = string.Empty;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var candidate = _pnrGenerator.Generate();
            if (!await _unitOfWork.Bookings.PnrExistsAsync(candidate, cancellationToken))
            {
                pnrCode = candidate;
                break;
            }
        }

        if (string.IsNullOrEmpty(pnrCode))
        {
            throw new InvalidOperationException("Failed to generate a unique PNR code. Please retry.");
        }

        // 8. Execute database persistence within an ACID transaction
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var booking = new Booking
            {
                PnrCode = pnrCode,
                UserId = request.UserId,
                TotalAmount = totalAmount,
                Status = BookingStatus.Confirmed,
                ContactEmail = request.Contact.Email.Trim(),
                ContactPhone = request.Contact.PhoneNumber.Trim(),
                ExpiresAtUtc = null
            };

            var payment = new Payment
            {
                Booking = booking,
                Amount = totalAmount,
                PaymentDate = utcNow,
                PaymentMethod = "CreditCard",
                TransactionReference = paymentResult.TransactionReference ?? Guid.NewGuid().ToString("N"),
                Status = PaymentStatus.Success
            };
            booking.Payment = payment;

            foreach (var p in request.Passengers)
            {
                var seat = aircraftSeatsById[p.SeatId];
                var passenger = new Passenger
                {
                    FirstName = p.FirstName.Trim(),
                    LastName = p.LastName.Trim(),
                    PassportNumber = p.PassportNumber.Trim().ToUpperInvariant(),
                    DateOfBirth = p.DateOfBirth.Date,
                    Gender = "Unspecified"
                };

                var ticket = new Ticket
                {
                    Booking = booking,
                    TicketNumber = $"ET-{pnrCode}-{seat.SeatNumber}",
                    FlightId = request.FlightId,
                    SeatId = seat.Id,
                    Passenger = passenger,
                    Price = seatPrices[seat.Id],
                    IsCancelled = false
                };

                booking.Tickets.Add(ticket);
            }

            await _unitOfWork.Bookings.AddAsync(booking, cancellationToken);

            // 9. Consume and delete user's temporary seat holds in the same transaction
            await _unitOfWork.SeatHolds.ReleaseUserHoldsForFlightAsync(request.FlightId, request.UserId, cancellationToken);

            // 10. Commit transaction (UnitOfWork catches SQL 2601/2627 and translates to UniqueConstraintViolationException)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var resultDto = new CheckoutResultDto
            {
                BookingId = booking.Id,
                PnrCode = booking.PnrCode,
                TotalAmount = booking.TotalAmount,
                Status = booking.Status.ToString(),
                CreatedAtUtc = utcNow
            };

            return ApiResponse<CheckoutResultDto>.SuccessResult(resultDto, "Booking successfully completed.");
        }
        catch (UniqueConstraintViolationException ex)
        {
            // UnitOfWork.CommitTransactionAsync already rolled back the transaction
            throw new ConflictException("One or more selected seats were just booked by another transaction.", ex);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
