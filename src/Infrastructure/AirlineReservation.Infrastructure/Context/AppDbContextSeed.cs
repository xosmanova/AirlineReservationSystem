using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Context;

public static class AppDbContextSeed
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // 1. Seed Airports
        if (!await context.Airports.AnyAsync())
        {
            var airports = new List<Airport>
            {
                new() { IataCode = "GYD", Name = "Heydar Aliyev International Airport", City = "Baku", Country = "Azerbaijan", TimeZone = "UTC+4" },
                new() { IataCode = "IST", Name = "Istanbul Airport", City = "Istanbul", Country = "Turkey", TimeZone = "UTC+3" },
                new() { IataCode = "SAW", Name = "Sabiha Gokcen International Airport", City = "Istanbul", Country = "Turkey", TimeZone = "UTC+3" },
                new() { IataCode = "DXB", Name = "Dubai International Airport", City = "Dubai", Country = "United Arab Emirates", TimeZone = "UTC+4" },
                new() { IataCode = "JFK", Name = "John F. Kennedy International Airport", City = "New York", Country = "United States", TimeZone = "UTC-5" },
                new() { IataCode = "LHR", Name = "Heathrow Airport", City = "London", Country = "United Kingdom", TimeZone = "UTC+0" },
                new() { IataCode = "FRA", Name = "Frankfurt Airport", City = "Frankfurt", Country = "Germany", TimeZone = "UTC+1" }
            };

            await context.Airports.AddRangeAsync(airports);
            await context.SaveChangesAsync();
        }

        // 2. Seed Airlines
        if (!await context.Airlines.AnyAsync())
        {
            var airlines = new List<Airline>
            {
                new() { Code = "J2", Name = "Azerbaijan Airlines (AZAL)", LogoUrl = "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?w=128" },
                new() { Code = "TK", Name = "Turkish Airlines", LogoUrl = "https://images.unsplash.com/photo-1436491865332-7a61a109cc05?w=128" },
                new() { Code = "EK", Name = "Emirates", LogoUrl = "https://images.unsplash.com/photo-1517479180992-0b2fcf3e8e24?w=128" },
                new() { Code = "LH", Name = "Lufthansa", LogoUrl = "https://images.unsplash.com/photo-1559297434-fae8a1916a79?w=128" }
            };

            await context.Airlines.AddRangeAsync(airlines);
            await context.SaveChangesAsync();
        }

        // 3. Seed Aircrafts
        if (!await context.Aircrafts.AnyAsync())
        {
            var azal = await context.Airlines.FirstAsync(a => a.Code == "J2");
            var thy = await context.Airlines.FirstAsync(a => a.Code == "TK");
            var emirates = await context.Airlines.FirstAsync(a => a.Code == "EK");

            var aircrafts = new List<Aircraft>
            {
                new() { Model = "Airbus A320-200", TailNumber = "4K-AZ01", TotalSeats = 150, AirlineId = azal.Id },
                new() { Model = "Boeing 737-800", TailNumber = "TC-JHK", TotalSeats = 162, AirlineId = thy.Id },
                new() { Model = "Boeing 777-300ER", TailNumber = "A6-ENJ", TotalSeats = 300, AirlineId = emirates.Id }
            };

            await context.Aircrafts.AddRangeAsync(aircrafts);
            await context.SaveChangesAsync();
        }

        // 4. Generate Seats for each aircraft
        if (!await context.Seats.AnyAsync())
        {
            var aircrafts = await context.Aircrafts.ToListAsync();
            var seats = new List<Seat>();

            foreach (var aircraft in aircrafts)
            {
                int totalRows = aircraft.Model.Contains("A320") ? 25 :
                                aircraft.Model.Contains("737") ? 27 : 35;

                for (int row = 1; row <= totalRows; row++)
                {
                    bool isBusiness = row <= 4;
                    bool isExitRow = (row == 12 || row == 13);
                    var letters = isBusiness ? new[] { "A", "C", "D", "F" } : new[] { "A", "B", "C", "D", "E", "F" };

                    foreach (var col in letters)
                    {
                        seats.Add(new Seat
                        {
                            AircraftId = aircraft.Id,
                            SeatNumber = $"{row}{col}",
                            RowNumber = row,
                            ColumnLetter = col,
                            SeatClass = isBusiness ? SeatClass.Business : SeatClass.Economy,
                            BasePriceMultiplier = isBusiness ? 2.5m : (isExitRow ? 1.25m : 1.0m),
                            IsWindow = col is "A" or "F",
                            IsAisle = col is "C" or "D",
                            IsExitRow = isExitRow
                        });
                    }
                }
            }

            await context.Seats.AddRangeAsync(seats);
            await context.SaveChangesAsync();
        }

        // 5. Seed Flights for the next 30 days
        if (!await context.Flights.AnyAsync())
        {
            var gyd = await context.Airports.FirstAsync(a => a.IataCode == "GYD");
            var ist = await context.Airports.FirstAsync(a => a.IataCode == "IST");
            var dxb = await context.Airports.FirstAsync(a => a.IataCode == "DXB");
            var jfk = await context.Airports.FirstAsync(a => a.IataCode == "JFK");
            var lhr = await context.Airports.FirstAsync(a => a.IataCode == "LHR");
            var fra = await context.Airports.FirstAsync(a => a.IataCode == "FRA");

            var azal = await context.Airlines.FirstAsync(a => a.Code == "J2");
            var thy = await context.Airlines.FirstAsync(a => a.Code == "TK");
            var emirates = await context.Airlines.FirstAsync(a => a.Code == "EK");

            var a320 = await context.Aircrafts.FirstAsync(a => a.Model.Contains("A320"));
            var b737 = await context.Aircrafts.FirstAsync(a => a.Model.Contains("737"));
            var b777 = await context.Aircrafts.FirstAsync(a => a.Model.Contains("777"));

            var flights = new List<Flight>();
            var today = DateTime.UtcNow.Date;

            for (int i = 0; i < 30; i++)
            {
                var flightDate = today.AddDays(i);

                // Flight 1: Baku -> Istanbul (AZAL morning)
                flights.Add(new Flight
                {
                    FlightNumber = $"J2-075-{i + 1:D2}",
                    AirlineId = azal.Id,
                    AircraftId = a320.Id,
                    DepartureAirportId = gyd.Id,
                    ArrivalAirportId = ist.Id,
                    DepartureTimeUtc = flightDate.AddHours(8).AddMinutes(30),
                    ArrivalTimeUtc = flightDate.AddHours(10).AddMinutes(45),
                    BasePrice = 185.00m,
                    Status = FlightStatus.Scheduled
                });

                // Flight 2: Istanbul -> Baku (AZAL afternoon)
                flights.Add(new Flight
                {
                    FlightNumber = $"J2-076-{i + 1:D2}",
                    AirlineId = azal.Id,
                    AircraftId = a320.Id,
                    DepartureAirportId = ist.Id,
                    ArrivalAirportId = gyd.Id,
                    DepartureTimeUtc = flightDate.AddHours(12).AddMinutes(15),
                    ArrivalTimeUtc = flightDate.AddHours(16).AddMinutes(20),
                    BasePrice = 185.00m,
                    Status = FlightStatus.Scheduled
                });

                // Flight 3: Baku -> Dubai (Emirates evening)
                flights.Add(new Flight
                {
                    FlightNumber = $"EK-219-{i + 1:D2}",
                    AirlineId = emirates.Id,
                    AircraftId = b777.Id,
                    DepartureAirportId = gyd.Id,
                    ArrivalAirportId = dxb.Id,
                    DepartureTimeUtc = flightDate.AddHours(16).AddMinutes(45),
                    ArrivalTimeUtc = flightDate.AddHours(19).AddMinutes(50),
                    BasePrice = 260.00m,
                    Status = FlightStatus.Scheduled
                });

                // Flight 4: Dubai -> Baku (Emirates night)
                flights.Add(new Flight
                {
                    FlightNumber = $"EK-220-{i + 1:D2}",
                    AirlineId = emirates.Id,
                    AircraftId = b777.Id,
                    DepartureAirportId = dxb.Id,
                    ArrivalAirportId = gyd.Id,
                    DepartureTimeUtc = flightDate.AddHours(21).AddMinutes(10),
                    ArrivalTimeUtc = flightDate.AddDays(1).AddHours(0).AddMinutes(25),
                    BasePrice = 260.00m,
                    Status = FlightStatus.Scheduled
                });

                // Flight 5: Istanbul -> New York (Turkish Airlines long-haul)
                flights.Add(new Flight
                {
                    FlightNumber = $"TK-001-{i + 1:D2}",
                    AirlineId = thy.Id,
                    AircraftId = b737.Id,
                    DepartureAirportId = ist.Id,
                    ArrivalAirportId = jfk.Id,
                    DepartureTimeUtc = flightDate.AddHours(6).AddMinutes(50),
                    ArrivalTimeUtc = flightDate.AddHours(11).AddMinutes(35),
                    BasePrice = 640.00m,
                    Status = FlightStatus.Scheduled
                });

                // Flight 6: London -> Frankfurt
                flights.Add(new Flight
                {
                    FlightNumber = $"LH-905-{i + 1:D2}",
                    AirlineId = thy.Id,
                    AircraftId = b737.Id,
                    DepartureAirportId = lhr.Id,
                    ArrivalAirportId = fra.Id,
                    DepartureTimeUtc = flightDate.AddHours(10).AddMinutes(20),
                    ArrivalTimeUtc = flightDate.AddHours(13).AddMinutes(00),
                    BasePrice = 145.00m,
                    Status = FlightStatus.Scheduled
                });
            }

            await context.Flights.AddRangeAsync(flights);
            await context.SaveChangesAsync();
        }
    }
}
