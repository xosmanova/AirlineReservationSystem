using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Commands.CancelBooking;

/// <summary>
/// Command handler that executes booking cancellation, updates ticket status, marks payments as refunded,
/// and applies policy-driven refunds within an ACID transaction.
/// </summary>
public class CancelBookingCommandHandler : IRequestHandler<CancelBookingCommand, ApiResponse<BookingDetailsDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICancellationPolicyService _cancellationPolicyService;
    private readonly TimeProvider _timeProvider;

    public CancelBookingCommandHandler(
        IUnitOfWork unitOfWork,
        ICancellationPolicyService cancellationPolicyService,
        TimeProvider? timeProvider = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cancellationPolicyService = cancellationPolicyService ?? throw new ArgumentNullException(nameof(cancellationPolicyService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiResponse<BookingDetailsDto>> Handle(
        CancelBookingCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(request.BookingId, trackChanges: true, cancellationToken);
        if (booking == null)
        {
            throw new NotFoundException($"Booking with ID {request.BookingId} was not found.");
        }

        // Ownership verification: non-admin users can only cancel their own bookings
        if (!request.IsAdmin && booking.UserId != request.UserId)
        {
            throw new ForbiddenException("You do not have permission to cancel this booking.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            throw new BadRequestException("Booking is already cancelled.");
        }

        var flight = booking.Tickets.FirstOrDefault()?.Flight;
        if (flight == null)
        {
            throw new BadRequestException("Cannot determine flight details for this booking.");
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var calculation = _cancellationPolicyService.CalculateRefund(booking.TotalAmount, flight.DepartureTimeUtc, nowUtc);

        if (!calculation.CanCancel)
        {
            throw new BadRequestException(calculation.Reason ?? "Booking cannot be cancelled.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            booking.Status = BookingStatus.Cancelled;
            booking.RefundAmount = calculation.RefundAmount;
            booking.CancelledAtUtc = nowUtc;

            foreach (var ticket in booking.Tickets)
            {
                ticket.IsCancelled = true;
                ticket.CancelledAtUtc = nowUtc;
            }

            if (booking.Payment != null)
            {
                booking.Payment.Status = PaymentStatus.Refunded;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var detailsDto = booking.ToDetailsDto();
        return ApiResponse<BookingDetailsDto>.SuccessResult(detailsDto, "Booking cancelled successfully.");
    }
}
