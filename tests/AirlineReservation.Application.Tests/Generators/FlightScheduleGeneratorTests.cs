using System.Text.RegularExpressions;
using AirlineReservation.Application.Common.Generators;
using Xunit;

namespace AirlineReservation.Application.Tests.Generators;

public class FlightScheduleGeneratorTests
{
    private readonly AirportCoordinates _gyd = new(1, "GYD", 40.4675, 50.0467);
    private readonly AirportCoordinates _ist = new(2, "IST", 41.2753, 28.7519);
    private readonly AirportCoordinates _saw = new(3, "SAW", 40.8986, 29.3092);
    private readonly AirportCoordinates _dxb = new(4, "DXB", 25.2532, 55.3657);
    private readonly AirportCoordinates _jfk = new(5, "JFK", 40.6413, -73.7781);
    private readonly AirportCoordinates _lhr = new(6, "LHR", 51.4700, -0.4543);
    private readonly AirportCoordinates _fra = new(7, "FRA", 50.0379, 8.5622);

    [Fact]
    public void CalculateHaversineDistanceKm_CalculatesAccurateGreatCircleDistance()
    {
        // GYD (Baku) to IST (Istanbul) great-circle distance is ~1788 km
        double distanceGydIst = FlightScheduleGenerator.CalculateHaversineDistanceKm(
            _gyd.Latitude, _gyd.Longitude, _ist.Latitude, _ist.Longitude);

        Assert.InRange(distanceGydIst, 1750, 1850);

        // DXB (Dubai) to LHR (London) is approximately 5500 km
        double distanceDxbLhr = FlightScheduleGenerator.CalculateHaversineDistanceKm(
            _dxb.Latitude, _dxb.Longitude, _lhr.Latitude, _lhr.Longitude);

        Assert.InRange(distanceDxbLhr, 5400, 5600);
    }

    [Fact]
    public void CalculateFlightDurationMinutes_FollowsFormula_AndRoundsTo5Minutes()
    {
        // 2250 km / 800 * 60 + 40 = 168.75 + 40 = 208.75 -> rounded to 5 = 210 min
        int durationDirect = FlightScheduleGenerator.CalculateFlightDurationMinutes(2250, stops: 0);
        Assert.Equal(210, durationDirect);
        Assert.Equal(0, durationDirect % 5);

        // With 1 stop: direct + 120 minutes = 330 min
        int duration1Stop = FlightScheduleGenerator.CalculateFlightDurationMinutes(2250, stops: 1);
        Assert.Equal(330, duration1Stop);
    }

    [Fact]
    public void CalculateBasePrice_FollowsFormula_AndApplies10PercentDiscountForStops()
    {
        // 40 + 2250 * 0.07 = 40 + 157.5 = 197.50
        decimal directPrice = FlightScheduleGenerator.CalculateBasePrice(2250, stops: 0);
        Assert.Equal(197.50m, directPrice);

        // 1-stop discount: 197.50 * 0.90 = 177.75
        decimal stopPrice = FlightScheduleGenerator.CalculateBasePrice(2250, stops: 1);
        Assert.Equal(177.75m, stopPrice);
    }

    [Fact]
    public void RoundUpTo15Minutes_RoundsProperly()
    {
        var t1 = new DateTime(2026, 11, 8, 7, 42, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 8, 7, 45, 0, DateTimeKind.Utc), FlightScheduleGenerator.RoundUpTo15Minutes(t1));

        var t2 = new DateTime(2026, 11, 8, 7, 45, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 8, 7, 45, 0, DateTimeKind.Utc), FlightScheduleGenerator.RoundUpTo15Minutes(t2));

        var t3 = new DateTime(2026, 11, 8, 7, 46, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc), FlightScheduleGenerator.RoundUpTo15Minutes(t3));
    }

    [Fact]
    public void GenerateScheduleForAircraft_HasNoOverlappingFlights_AndGuaranteesLocationContinuity()
    {
        // Arrange: J2 A320 based at GYD with routes to IST, SAW, DXB
        var request = new AircraftScheduleRequest(
            AircraftId: 1,
            AirlineId: 1,
            AirlineCode: "J2",
            HubAirport: _gyd,
            DestinationAirports: [_ist, _saw, _dxb],
            StartDateUtc: new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc),
            TotalDays: 14,
            BaseFlightNumber: 101
        );

        // Act
        var flights = FlightScheduleGenerator.GenerateScheduleForAircraft(request);

        // Assert:
        Assert.NotEmpty(flights);

        for (int i = 0; i < flights.Count; i++)
        {
            var flight = flights[i];

            // 1. Departure is strictly before Arrival
            Assert.True(flight.DepartureTimeUtc < flight.ArrivalTimeUtc,
                $"Flight {flight.FlightNumber} departs after it arrives.");

            // 2. FlightNumber format: Airline code + 3 digits
            Assert.Matches(@"^J2\d{3}$", flight.FlightNumber);

            // 3. Departure is inside the 02:00-20:00 UTC window
            Assert.True(flight.DepartureTimeUtc.TimeOfDay >= TimeSpan.FromHours(2),
                $"Flight {flight.FlightNumber} departs before 02:00 UTC: {flight.DepartureTimeUtc.TimeOfDay}");
            Assert.True(flight.DepartureTimeUtc.TimeOfDay <= TimeSpan.FromHours(20),
                $"Flight {flight.FlightNumber} departs after 20:00 UTC: {flight.DepartureTimeUtc.TimeOfDay}");

            // 4. Departure minute is a multiple of 15
            Assert.Equal(0, flight.DepartureTimeUtc.Minute % 15);

            // 5. Inter-flight checks
            if (i > 0)
            {
                var previousFlight = flights[i - 1];

                // Location continuity: Departure airport equals previous flight's Arrival airport
                Assert.Equal(previousFlight.ArrivalAirportId, flight.DepartureAirportId);

                // Turnaround constraint: Departure is at least 90 minutes after previous arrival
                var minAllowedDeparture = previousFlight.ArrivalTimeUtc.AddMinutes(90);
                Assert.True(flight.DepartureTimeUtc >= minAllowedDeparture,
                    $"Flight {flight.FlightNumber} departs too early ({flight.DepartureTimeUtc:u}) after previous flight arrived at {previousFlight.ArrivalTimeUtc:u}. Minimum required: {minAllowedDeparture:u}");
            }
        }
    }

    [Fact]
    public void GenerateSchedule_All8Aircraft_NoOverlapsAndAll4AirlinesRepresented()
    {
        // 8 Aircraft specifications from Step 5
        var requests = new List<AircraftScheduleRequest>
        {
            // J2 A320-200 #1, hub GYD
            new(1, 1, "J2", _gyd, [_ist, _saw, _dxb], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, BaseFlightNumber: 101),
            // J2 A320-200 #2, hub GYD (offset start)
            new(2, 1, "J2", _gyd, [_saw, _dxb, _ist], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, InitialDepartureOffset: TimeSpan.FromHours(3), BaseFlightNumber: 111),

            // TK 737-800 #1, hub IST
            new(3, 2, "TK", _ist, [_gyd, _lhr, _fra, _dxb], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, BaseFlightNumber: 201),
            // TK 737-800 #2, hub GYD: GYD-LHR with Stops=1
            new(4, 2, "TK", _gyd, [_lhr], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, Stops: 1, InitialDepartureOffset: TimeSpan.FromHours(1), BaseFlightNumber: 221),

            // EK 777-300ER #1, hub DXB
            new(5, 3, "EK", _dxb, [_jfk, _lhr, _ist, _gyd], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, BaseFlightNumber: 301),
            // EK 777-300ER #2, hub DXB
            new(6, 3, "EK", _dxb, [_lhr, _ist, _gyd, _jfk], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, InitialDepartureOffset: TimeSpan.FromHours(4), BaseFlightNumber: 321),

            // LH A321 #1, hub FRA
            new(7, 4, "LH", _fra, [_lhr, _ist, _gyd], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, BaseFlightNumber: 401),
            // LH A321 #2, hub FRA
            new(8, 4, "LH", _fra, [_ist, _gyd, _lhr], new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), 7, InitialDepartureOffset: TimeSpan.FromHours(2), BaseFlightNumber: 421)
        };

        var allFlights = new List<GeneratedFlight>();

        foreach (var req in requests)
        {
            var flights = FlightScheduleGenerator.GenerateScheduleForAircraft(req);
            allFlights.AddRange(flights);

            // Assert NO overlaps on THIS aircraft
            for (int i = 1; i < flights.Count; i++)
            {
                Assert.True(flights[i].DepartureTimeUtc >= flights[i - 1].ArrivalTimeUtc.AddMinutes(90),
                    $"Aircraft {req.AircraftId} has overlapping or insufficiently spaced flights.");
                Assert.Equal(flights[i - 1].ArrivalAirportId, flights[i].DepartureAirportId);
            }
        }

        // Assert all 4 airlines have flights
        var airlineCodes = allFlights.Select(f => f.FlightNumber[..2]).Distinct().ToHashSet();
        Assert.Contains("J2", airlineCodes);
        Assert.Contains("TK", airlineCodes);
        Assert.Contains("EK", airlineCodes);
        Assert.Contains("LH", airlineCodes);

        // Assert TK #2 has Stops=1
        var tk2Flights = allFlights.Where(f => f.AircraftId == 4).ToList();
        Assert.NotEmpty(tk2Flights);
        Assert.All(tk2Flights, f => Assert.Equal(1, f.Stops));
    }
}
