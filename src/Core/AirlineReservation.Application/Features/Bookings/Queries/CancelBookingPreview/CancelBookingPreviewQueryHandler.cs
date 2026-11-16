using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.CancelBookingPreview;

/// <summary>
/// Query handler that previews cancellation eligibility, refund percentages, and penalty amounts.
/// </summary>
public class CancelBookingPreviewQueryHandler : IRequestHandler<CancelBookingPreviewQuery, ApiResponse<CancelBookingPreviewDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICancellationPolicyService _cancellationPolicyService;
    private readonly TimeProvider _timeProvider;

    public CancelBookingPreviewQueryHandler(
        IUnitOfWork unitOfWork,
        ICancellationPolicyService cancellationPolicyService,
        TimeProvider? timeProvider = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cancellationPolicyService = cancellationPolicyService ?? throw new ArgumentNullException(nameof(cancellationPolicyService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiResponse<CancelBookingPreviewDto>> Handle(
        CancelBookingPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(request.BookingId, trackChanges: false, cancellationToken);
        if (booking == null)
        {
            throw new NotFoundException($"Booking with ID {request.BookingId} was not found.");
        }

        // Ownership verification: only the owner or an admin can preview cancellation
        if (!request.IsAdmin && booking.UserId != request.UserId)
        {
            throw new ForbiddenException("You do not have permission to view cancellation preview for this booking.");
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

        var preview = new CancelBookingPreviewDto
        {
            BookingId = booking.Id,
            PnrCode = booking.PnrCode,
            TotalAmount = booking.TotalAmount,
            RefundPercentage = calculation.RefundPercentage,
            RefundAmount = calculation.RefundAmount,
            PenaltyAmount = calculation.PenaltyAmount,
            CanCancel = calculation.CanCancel,
            Reason = calculation.Reason ?? string.Empty,
            DepartureTimeUtc = flight.DepartureTimeUtc
        };

        return ApiResponse<CancelBookingPreviewDto>.SuccessResult(preview, "Cancellation preview calculated successfully.");
    }
}
