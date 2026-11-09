using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Application.DTOs.Airline;
using MediatR;

namespace AirlineReservation.Application.Features.Airlines.Queries.GetAllAirlines;

/// <summary>
/// CQRS handler for retrieving all registered airlines.
/// </summary>
public class GetAllAirlinesQueryHandler : IRequestHandler<GetAllAirlinesQuery, ApiResponse<IReadOnlyList<AirlineDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AirlineMappingProfile _mapper;

    public GetAllAirlinesQueryHandler(IUnitOfWork unitOfWork, AirlineMappingProfile mapper)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<ApiResponse<IReadOnlyList<AirlineDto>>> Handle(GetAllAirlinesQuery request, CancellationToken cancellationToken)
    {
        var airlines = await _unitOfWork.Airlines.GetAllOrderedAsync(cancellationToken);
        var dtos = _mapper.ToDtoList(airlines);

        var message = dtos.Count > 0
            ? $"Found {dtos.Count} airline(s)."
            : "No airlines found.";

        return ApiResponse<IReadOnlyList<AirlineDto>>.SuccessResult(dtos, message);
    }
}
