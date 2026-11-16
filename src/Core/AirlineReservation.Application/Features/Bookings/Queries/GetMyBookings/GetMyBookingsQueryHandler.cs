using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Domain.Enums;
using MediatR;

namespace AirlineReservation.Application.Features.Bookings.Queries.GetMyBookings;

/// <summary>
/// Handles user trip itinerary queries with status filtering and pagination.
/// </summary>
public class GetMyBookingsQueryHandler : IRequestHandler<GetMyBookingsQuery, ApiResponse<PagedResult<BookingDetailsDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public GetMyBookingsQueryHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ApiResponse<PagedResult<BookingDetailsDto>>> Handle(
        GetMyBookingsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bookings = await _unitOfWork.Bookings.GetBookingsByUserIdAsync(request.UserId, cancellationToken);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var query = bookings.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusFilter = request.Status.Trim().ToLowerInvariant();
            query = statusFilter switch
            {
                "upcoming" => query.Where(b => b.Status != BookingStatus.Cancelled &&
                                               (b.Tickets.FirstOrDefault()?.Flight?.DepartureTimeUtc ?? DateTime.MinValue) > nowUtc),

                "past" => query.Where(b => b.Status != BookingStatus.Cancelled &&
                                           (b.Tickets.FirstOrDefault()?.Flight?.DepartureTimeUtc ?? DateTime.MaxValue) <= nowUtc),

                "cancelled" => query.Where(b => b.Status == BookingStatus.Cancelled),

                _ => query
            };
        }

        var filteredList = query.ToList();
        var totalCount = filteredList.Count;

        var pagedItems = filteredList
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => b.ToDetailsDto())
            .ToList();

        var pagedResult = new PagedResult<BookingDetailsDto>(
            pagedItems,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return ApiResponse<PagedResult<BookingDetailsDto>>.SuccessResult(pagedResult, "User bookings retrieved successfully.");
    }
}
