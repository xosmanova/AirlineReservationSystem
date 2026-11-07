namespace AirlineReservation.Application.Common.Generators;

public sealed record AirportCoordinates(
    int Id,
    string IataCode,
    double Latitude,
    double Longitude
);

public sealed record AircraftScheduleRequest(
    int AircraftId,
    int AirlineId,
    string AirlineCode,
    AirportCoordinates HubAirport,
    IReadOnlyList<AirportCoordinates> DestinationAirports,
    DateTime StartDateUtc,
    int TotalDays = 30,
    int Stops = 0,
    TimeSpan? InitialDepartureOffset = null,
    int BaseFlightNumber = 101
);

public sealed record GeneratedFlight(
    string FlightNumber,
    int AirlineId,
    int AircraftId,
    int DepartureAirportId,
    int ArrivalAirportId,
    string DepartureIata,
    string ArrivalIata,
    DateTime DepartureTimeUtc,
    DateTime ArrivalTimeUtc,
    decimal BasePrice,
    int Stops,
    int DurationMinutes
);

public static class FlightScheduleGenerator
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Calculates Great-Circle (Haversine) distance between two sets of GPS coordinates in kilometers.
    /// </summary>
    public static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// DurationMinutes = round(haversine(distance)/800*60+40) to 5 minutes.
    /// If stops > 0, adds 120 minutes per intermediate stop.
    /// </summary>
    public static int CalculateFlightDurationMinutes(double distanceKm, int stops = 0)
    {
        double rawMinutes = (distanceKm / 800.0 * 60.0) + 40.0;
        int rounded = (int)(Math.Round(rawMinutes / 5.0, MidpointRounding.AwayFromZero) * 5);
        if (rounded < 45) rounded = 45;

        if (stops > 0)
        {
            rounded += stops * 120;
        }

        return rounded;
    }

    /// <summary>
    /// BasePrice = round(40 + distanceKm*0.07).
    /// If stops > 0 (e.g. 1-stop route), price is 10% lower.
    /// </summary>
    public static decimal CalculateBasePrice(double distanceKm, int stops = 0)
    {
        decimal rawPrice = 40.0m + (decimal)distanceKm * 0.07m;
        decimal price = Math.Round(rawPrice, 2, MidpointRounding.AwayFromZero);

        if (stops > 0)
        {
            price = Math.Round(price * 0.90m, 2, MidpointRounding.AwayFromZero);
        }

        return price;
    }

    /// <summary>
    /// Rounds a UTC DateTime UP to the nearest 15-minute boundary (:00, :15, :30, :45).
    /// </summary>
    public static DateTime RoundUpTo15Minutes(DateTime dt)
    {
        var clean = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Utc);
        int rem = clean.Minute % 15;
        if (rem > 0 || dt.Second > 0 || dt.Millisecond > 0)
        {
            clean = clean.AddMinutes(15 - rem);
        }
        return clean;
    }

    /// <summary>
    /// Computes the earliest departure time at or after (previous arrival + 90 min),
    /// rounded up to 15 minutes, inside the 02:00-20:00 UTC window (otherwise next day 02:00).
    /// </summary>
    public static DateTime CalculateNextDepartureUtc(DateTime previousArrivalUtc)
    {
        var earliestAllowed = previousArrivalUtc.AddMinutes(90);
        var candidate = RoundUpTo15Minutes(earliestAllowed);

        // Window: 02:00 to 20:00 UTC
        var windowStart = candidate.Date.AddHours(2);
        var windowEnd = candidate.Date.AddHours(20);

        if (candidate < windowStart)
        {
            candidate = windowStart;
        }
        else if (candidate > windowEnd)
        {
            candidate = candidate.Date.AddDays(1).AddHours(2);
        }

        // Safety guarantee: next departure can never be before earliestAllowed
        if (candidate < earliestAllowed)
        {
            candidate = RoundUpTo15Minutes(earliestAllowed);
            if (candidate > candidate.Date.AddHours(20))
            {
                candidate = candidate.Date.AddDays(1).AddHours(2);
            }
        }

        return candidate;
    }

    /// <summary>
    /// Generates non-overlapping sequential rotations for a single aircraft.
    /// Rotation pattern: Hub -> Destination (round-robin) -> Hub.
    /// </summary>
    public static IReadOnlyList<GeneratedFlight> GenerateScheduleForAircraft(AircraftScheduleRequest request)
    {
        if (request.DestinationAirports.Count == 0)
            throw new ArgumentException("At least one destination airport must be provided.", nameof(request));

        var flights = new List<GeneratedFlight>();
        var endDate = request.StartDateUtc.Date.AddDays(request.TotalDays);

        // Initial departure time inside the window (02:00 UTC + optional offset)
        var currentDeparture = request.StartDateUtc.Date.AddHours(2);
        if (request.InitialDepartureOffset.HasValue)
        {
            currentDeparture = currentDeparture.Add(request.InitialDepartureOffset.Value);
        }
        currentDeparture = RoundUpTo15Minutes(currentDeparture);

        int destIndex = 0;
        bool isOutbound = true;
        int flightCounter = request.BaseFlightNumber;

        while (currentDeparture < endDate)
        {
            var fromAirport = isOutbound ? request.HubAirport : request.DestinationAirports[destIndex];
            var toAirport = isOutbound ? request.DestinationAirports[destIndex] : request.HubAirport;

            double distance = CalculateHaversineDistanceKm(
                fromAirport.Latitude, fromAirport.Longitude,
                toAirport.Latitude, toAirport.Longitude
            );

            int duration = CalculateFlightDurationMinutes(distance, request.Stops);
            var arrival = currentDeparture.AddMinutes(duration);
            decimal price = CalculateBasePrice(distance, request.Stops);

            // Flight number: e.g. J2101, TK201, EK301, LH401 (airline code + 3 digits)
            int normalizedFlightNum = 100 + ((flightCounter - 100) % 900);
            string flightNumber = $"{request.AirlineCode}{normalizedFlightNum:D3}";
            flightCounter++;

            flights.Add(new GeneratedFlight(
                FlightNumber: flightNumber,
                AirlineId: request.AirlineId,
                AircraftId: request.AircraftId,
                DepartureAirportId: fromAirport.Id,
                ArrivalAirportId: toAirport.Id,
                DepartureIata: fromAirport.IataCode,
                ArrivalIata: toAirport.IataCode,
                DepartureTimeUtc: currentDeparture,
                ArrivalTimeUtc: arrival,
                BasePrice: price,
                Stops: request.Stops,
                DurationMinutes: duration
            ));

            if (!isOutbound)
            {
                // Returned to hub, advance to next destination in round-robin
                destIndex = (destIndex + 1) % request.DestinationAirports.Count;
            }

            isOutbound = !isOutbound;
            currentDeparture = CalculateNextDepartureUtc(arrival);
        }

        return flights;
    }
}
