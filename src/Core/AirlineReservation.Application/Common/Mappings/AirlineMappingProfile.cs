using AirlineReservation.Application.DTOs.Airline;
using AirlineReservation.Application.DTOs.Airport;
using AirlineReservation.Application.DTOs.Flight;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace AirlineReservation.Application.Common.Mappings;

#pragma warning disable RMG020 // Source member not mapped to target
#pragma warning disable RMG012 // Target member not mapped from source

/// <summary>
/// High-performance compile-time object mapper powered by Riok.Mapperly source generator.
/// Operates with zero reflection and zero runtime overhead.
/// </summary>
[Mapper]
public partial class AirlineMappingProfile
{
    public partial AirportDto ToDto(Airport entity);
    public partial IReadOnlyList<AirportDto> ToDtoList(IEnumerable<Airport> entities);

    public partial AirlineDto ToDto(Airline entity);
    public partial IReadOnlyList<AirlineDto> ToDtoList(IEnumerable<Airline> entities);

    public partial SeatDto ToDto(Seat entity);
    public partial IReadOnlyList<SeatDto> ToDtoList(IEnumerable<Seat> entities);

    public partial FlightDto ToDto(Flight entity);
    public partial IReadOnlyList<FlightDto> ToDtoList(IEnumerable<Flight> entities);
}
#pragma warning restore RMG020
#pragma warning restore RMG012
