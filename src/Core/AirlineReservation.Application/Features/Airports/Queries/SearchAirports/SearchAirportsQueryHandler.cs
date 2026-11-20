using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Airport;
using MediatR;

namespace AirlineReservation.Application.Features.Airports.Queries.SearchAirports;

/// <summary>
/// CQRS handler for airport autocomplete and lookup search queries.
/// </summary>
public class SearchAirportsQueryHandler : IRequestHandler<SearchAirportsQuery, ApiResponse<IReadOnlyList<AirportDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AirlineMappingProfile _mapper;

    public SearchAirportsQueryHandler(IUnitOfWork unitOfWork, AirlineMappingProfile mapper)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<ApiResponse<IReadOnlyList<AirportDto>>> Handle(SearchAirportsQuery request, CancellationToken cancellationToken)
    {
        var airports = await _unitOfWork.Airports.SearchAsync(request.Search, request.Limit, cancellationToken);
        var dtos = _mapper.ToDtoList(airports);

        var message = dtos.Count > 0
            ? $"Found {dtos.Count} airport(s)."
            : "No airports found matching the specified search criteria.";

        return ApiResponse<IReadOnlyList<AirportDto>>.SuccessResult(dtos, message);
    }
}
