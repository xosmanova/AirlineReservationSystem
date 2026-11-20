using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.DTOs.Airline;
using AirlineReservation.Application.DTOs.Airport;
using AirlineReservation.Application.DTOs.Passenger;
using AirlineReservation.Domain.Entities;
using Xunit;

namespace AirlineReservation.Application.Tests.Mappings;

public class AirlineMappingProfileTests
{
    private readonly AirlineMappingProfile _mapper = new();

    [Fact]
    public void Passenger_Mapping_NormalizesOnlyPassportNumber_AndPreservesNameCasing()
    {
        // Arrange
        var dto = new PassengerCreateDto
        {
            FirstName = "Əli",
            LastName = "Məmmədov",
            PassportNumber = "  aze123456  ",
            DateOfBirth = new DateTime(1995, 5, 20),
            SeatId = 1
        };

        // Act
        var passenger = _mapper.ToEntity(dto);

        // Assert
        Assert.NotNull(passenger);
        // Guarantee that NormalizePassport was not applied as default to Name strings
        Assert.Equal("Əli", passenger.FirstName);
        Assert.Equal("Məmmədov", passenger.LastName);

        // Only passport number should be trimmed and upper-cased
        Assert.Equal("AZE123456", passenger.PassportNumber);
    }

    [Fact]
    public void Airport_And_Airline_Mapping_PreservesStringCasing()
    {
        // Arrange
        var airport = new Airport
        {
            Id = 1,
            IataCode = "GYD",
            Name = "Heydar Aliyev International Airport",
            City = "Baku",
            Country = "Azerbaijan",
            TimeZone = "Asia/Baku",
            Aliases = "Baku,Bakı,Baki,Баку"
        };

        var airline = new Airline
        {
            Id = 1,
            Code = "J2",
            Name = "Azerbaijan Airlines (AZAL)",
            LogoUrl = "https://azal.az/logo.png"
        };

        // Act
        var airportDto = _mapper.ToDto(airport);
        var airlineDto = _mapper.ToDto(airline);

        // Assert
        Assert.Equal("Heydar Aliyev International Airport", airportDto.Name);
        Assert.Equal("Baku", airportDto.City);
        Assert.Equal("Azerbaijan", airportDto.Country);
        Assert.Equal("Baku,Bakı,Baki,Баку", airportDto.Aliases);

        Assert.Equal("Azerbaijan Airlines (AZAL)", airlineDto.Name);
        Assert.Equal("J2", airlineDto.Code);
    }
}
