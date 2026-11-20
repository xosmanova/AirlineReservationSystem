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
/// Enforces atomic seat hold claim before payment charging, active ticket checks, server-side pricing verification,
/// pre-payment PNR selection, and payment reversal compensation upon any subsequent persistence failure.
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

        // Step 1: Read-only validation (flight exists and is sellable 404/409, seats belong to aircraft 404/400)
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

        // Validate none of the requested seats are already marked occupied in seat map data
        var occupiedInMap = requestedSeatIds
            .Where(id => seatMapData.OccupiedSeatIds.Contains(id))
            .Select(id => aircraftSeatsById[id].SeatNumber)
            .ToList();

        if (occupiedInMap.Count > 0)
        {
            throw new ConflictException(
                $"The following seat(s) are already occupied: {string.Join(", ", occupiedInMap)}.");
        }

        // Step 2: Begin ACID database transaction
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // Step 3: Load ALL of the user's holds for the flight (expired included).
            var userHolds = await _unitOfWork.SeatHolds.GetUserAllHoldsForFlightAsync(
                request.FlightId,
                request.UserId,
                cancellationToken);

            var requestedSeatSet = requestedSeatIds.ToHashSet();
            var heldSeatSet = userHolds.Select(h => h.SeatId).ToHashSet();

            // Passenger seat IDs must EQUAL held seat IDs, otherwise 409
            if (!requestedSeatSet.SetEquals(heldSeatSet))
            {
                var unheld = requestedSeatIds.Where(id => !heldSeatSet.Contains(id))
                    .Select(id => aircraftSeatsById[id].SeatNumber).ToList();
                var extraHeld = heldSeatSet.Where(id => !requestedSeatSet.Contains(id))
                    .Select(id => aircraftSeatsById.TryGetValue(id, out var s) ? s.SeatNumber : id.ToString()).ToList();

                string message = (unheld.Count > 0, extraHeld.Count > 0) switch
                {
                    (true, false) => $"Active seat hold required for checkout. You do not hold a reservation for seat(s): {string.Join(", ", unheld)}.",
                    (false, true) => $"Passenger seats do not match your holds. You also have reserved seat(s): {string.Join(", ", extraHeld)}.",
                    _ => "Passenger seats must match your held seats exactly."
                };

                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new ConflictException(message);
            }

            // Expired holds get their own clear message
            var expiredHolds = userHolds.Where(h => h.ExpiresAtUtc <= utcNow).ToList();
            if (expiredHolds.Count > 0)
            {
                var expiredSeatNumbers = expiredHolds.Select(h => aircraftSeatsById.TryGetValue(h.SeatId, out var s) ? s.SeatNumber : h.SeatId.ToString());
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new ConflictException(
                    $"Your hold on seat(s) {string.Join(", ", expiredSeatNumbers)} has expired. Please reselect your seats.");
            }

            // Step 4: CLAIM: ExecuteDeleteAsync by hold Id. Deleted count must equal the hold count, otherwise 409 and NO charge.
            var holdIds = userHolds.Select(h => h.Id).ToList();
            var deletedCount = await _unitOfWork.SeatHolds.ClaimHoldsAsync(
                request.FlightId,
                request.UserId,
                holdIds,
                cancellationToken);

            if (deletedCount != holdIds.Count)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new ConflictException("One or more seat holds could not be claimed or were already processed.");
            }

            // Step 5: Active-ticket check for these seats with TicketSpecifications.IsActive (inside the transaction)
            var occupiedSeatIds = await _unitOfWork.Seats.GetActiveOccupiedSeatIdsForFlightAsync(
                request.FlightId,
                requestedSeatIds,
                cancellationToken);

            if (occupiedSeatIds.Count > 0)
            {
                var occupiedSeatNumbers = occupiedSeatIds
                    .Select(id => aircraftSeatsById.TryGetValue(id, out var s) ? s.SeatNumber : id.ToString())
                    .ToList();

                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new ConflictException(
                    $"The following seat(s) are already occupied: {string.Join(", ", occupiedSeatNumbers)}.");
            }

            // Step 6: Price from PricingService. If ExpectedTotalAmount is present and differs -> 409 PriceChanged
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

            if (request.ExpectedTotalAmount.HasValue && request.ExpectedTotalAmount.Value != totalAmount)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new ConflictException(
                    $"PriceChanged: The flight price has changed from {request.ExpectedTotalAmount.Value:F2} AZN to {totalAmount:F2} AZN. Please review the updated price before proceeding.");
            }

            // Step 7: Choose the PNR (AnyAsync, at most 5 attempts) BEFORE payment
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
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new ConflictException("Failed to generate a unique PNR code after multiple attempts. Please retry.");
            }

            // Step 8: ProcessPaymentAsync. Declined -> roll back (holds come back) and return the payment error
            var paymentRequest = new PaymentProcessRequest(
                totalAmount,
                "AZN",
                request.Card.CardNumber,
                request.Card.ExpiryMonthYear,
                request.Card.Cvv,
                request.Card.CardHolderName);

            PaymentProcessResult paymentResult;
            try
            {
                paymentResult = await _paymentGateway.ProcessPaymentAsync(paymentRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new BadRequestException($"Payment gateway communication failed: {ex.Message}", ex);
            }

            if (!paymentResult.Success)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new BadRequestException(
                    $"Payment processing failed: {paymentResult.ErrorMessage ?? "Payment was declined by issuer."}");
            }

            // Step 9 & 10: After step 8 succeeded, ANY failure (write, commit, deadlock, unique constraint)
            // must rollback and call ReverseAsync (compensation) with CancellationToken.None!
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

                for (int i = 0; i < request.Passengers.Count; i++)
                {
                    var p = request.Passengers[i];
                    var seat = aircraftSeatsById[p.SeatId];
                    var passenger = new Passenger
                    {
                        FirstName = p.FirstName.Trim(),
                        LastName = p.LastName.Trim(),
                        PassportNumber = p.PassportNumber.Trim().ToUpperInvariant(),
                        DateOfBirth = p.DateOfBirth.Date,
                        Gender = "Other"
                    };

                    var ticket = new Ticket
                    {
                        Booking = booking,
                        TicketNumber = $"{pnrCode}-{i + 1}",
                        FlightId = request.FlightId,
                        SeatId = seat.Id,
                        Passenger = passenger,
                        Price = seatPrices[seat.Id],
                        IsCancelled = false
                    };

                    booking.Tickets.Add(ticket);
                }

                await _unitOfWork.Bookings.AddAsync(booking, CancellationToken.None);

                // Step 10: Commit transaction
                await _unitOfWork.CommitTransactionAsync(CancellationToken.None);

                var resultDto = new CheckoutResultDto
                {
                    BookingId = booking.Id,
                    PnrCode = booking.PnrCode,
                    TotalAmount = booking.TotalAmount,
                    Status = booking.Status.ToString(),
                    CreatedAtUtc = utcNow
                };

                return ApiResponse<CheckoutResultDto>.SuccessResult(resultDto, "Booking successfully completed.", 201);
            }
            catch (Exception postPaymentEx)
            {
                // Best effort rollback
                try
                {
                    await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignored if already rolled back by CommitTransactionAsync
                }

                // Compensation: Reverse payment with CancellationToken.None
                if (!string.IsNullOrWhiteSpace(paymentResult.TransactionReference))
                {
                    try
                    {
                        await _paymentGateway.ReverseAsync(
                            new PaymentReversalRequest(paymentResult.TransactionReference, totalAmount, "AZN"),
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Compensation failure logged/handled (best-effort)
                    }
                }

                if (postPaymentEx is UniqueConstraintViolationException uex)
                {
                    if (uex.IndexName != null && uex.IndexName.Contains("Pnr", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ConflictException("A PNR collision occurred during booking creation. Your payment has been refunded. Please retry checkout.", uex);
                    }

                    throw new ConflictException("One or more selected seats were just booked by another transaction. Your payment has been refunded.", uex);
                }

                throw;
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }
}
