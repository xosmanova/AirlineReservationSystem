using System;
using System.Linq;
using System.Threading.Tasks;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineReservation.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Server=.\\SQLEXPRESS;Database=AirlineReservationDb_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    public int TestFlight1Id { get; private set; }
    public int TestFlight2DelayedId { get; private set; }
    public int TestFlight3CancelledId { get; private set; }
    public int TestDepartureAirportId { get; private set; }
    public int TestArrivalAirportId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing AppDbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Register AppDbContext with isolated test database
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(TestConnectionString));

            // Replace TimeProvider with a deterministic clock before test flight departure (08:00Z on 2026-11-10)
            var timeDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TimeProvider));
            if (timeDescriptor != null)
            {
                services.Remove(timeDescriptor);
            }
            services.AddSingleton<TimeProvider>(
                new FixedTimeProvider(new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero)));
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task ClearTestDataAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.ExecuteSqlRawAsync(@"
            DELETE FROM [Tickets];
            DELETE FROM [Bookings];
            DELETE FROM [Passengers];
            DELETE FROM [Seats];
            DELETE FROM [Flights];
            DELETE FROM [Aircrafts];
            DELETE FROM [Airlines];
            DELETE FROM [Airports];
        ");
    }

    public async Task SeedTestDataAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Airports
        var gyd = new Airport { IataCode = "GYD", Name = "Heydar Aliyev Int.", City = "Baku", Country = "Azerbaijan", TimeZone = "Asia/Baku", Aliases = "Baku,Bakı,Baki,Баку" };
        var ist = new Airport { IataCode = "IST", Name = "Istanbul Airport", City = "Istanbul", Country = "Turkey", TimeZone = "Europe/Istanbul", Aliases = "Istanbul,İstanbul,Istanbul Airport,Стамбул" };
        var saw = new Airport { IataCode = "SAW", Name = "Sabiha Gokcen International Airport", City = "Istanbul", Country = "Turkey", TimeZone = "Europe/Istanbul", Aliases = "Istanbul,İstanbul,Sabiha Gökçen,Sabiha Gokcen,Стамбул" };
        context.Airports.AddRange(gyd, ist, saw);

        // 2. Airline
        var azal = new Airline { Code = "J2", Name = "Azerbaijan Airlines", LogoUrl = "https://azal.az/logo.png" };
        var thy = new Airline { Code = "TK", Name = "Turkish Airlines", LogoUrl = "https://turkishairlines.com/logo.png" };
        context.Airlines.AddRange(azal, thy);

        // 3. Aircrafts
        var a320 = new Aircraft { Model = "Airbus A320", TailNumber = "TEST-A320-1", TotalSeats = 2, Airline = azal };
        var b738 = new Aircraft { Model = "Boeing 737-800", TailNumber = "TEST-B738-1", TotalSeats = 180, Airline = azal };
        context.Aircrafts.AddRange(a320, b738);

        // 4. Seats for a320 (2 seats)
        var s1 = new Seat { Aircraft = a320, SeatNumber = "1A", RowNumber = 1, ColumnLetter = "A", SeatClass = SeatClass.Economy };
        var s2 = new Seat { Aircraft = a320, SeatNumber = "1B", RowNumber = 1, ColumnLetter = "B", SeatClass = SeatClass.Economy };
        context.Seats.AddRange(s1, s2);

        // Reference base date in Baku time: 2026-11-10
        // Local day bounds in UTC: 2026-11-09 20:00:00 UTC to 2026-11-10 20:00:00 UTC
        var depTime = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var arrTime = new DateTime(2026, 11, 10, 11, 0, 0, DateTimeKind.Utc);

        // Flight 1: Scheduled, TotalSeats = 2, has 1 active ticket (so 1 available seat)
        var f1 = new Flight
        {
            FlightNumber = "J2-101",
            Airline = azal,
            Aircraft = a320,
            DepartureAirport = gyd,
            ArrivalAirport = ist,
            DepartureTimeUtc = depTime,
            ArrivalTimeUtc = arrTime,
            BasePrice = 150m,
            Stops = 0,
            Status = FlightStatus.Scheduled
        };

        // Flight 2: Delayed status (should be sellable/returned)
        var f2 = new Flight
        {
            FlightNumber = "J2-102",
            Airline = azal,
            Aircraft = b738,
            DepartureAirport = gyd,
            ArrivalAirport = ist,
            DepartureTimeUtc = depTime.AddHours(2),
            ArrivalTimeUtc = arrTime.AddHours(2),
            BasePrice = 200m,
            Stops = 0,
            Status = FlightStatus.Delayed
        };

        // Flight 3: Cancelled status (must NOT be returned)
        var f3 = new Flight
        {
            FlightNumber = "J2-103",
            Airline = azal,
            Aircraft = b738,
            DepartureAirport = gyd,
            ArrivalAirport = ist,
            DepartureTimeUtc = depTime.AddHours(4),
            ArrivalTimeUtc = arrTime.AddHours(4),
            BasePrice = 180m,
            Stops = 0,
            Status = FlightStatus.Cancelled
        };

        context.Flights.AddRange(f1, f2, f3);

        // Booking & Ticket for Flight 1 (Seat 1A occupied, leaving 1 free seat on Flight 1)
        var booking = new Booking
        {
            PnrCode = "TEST01",
            ContactEmail = "test@example.com",
            ContactPhone = "+994501234567",
            Status = BookingStatus.Confirmed,
            TotalAmount = 150m
        };
        context.Bookings.Add(booking);

        var passenger = new Passenger
        {
            FirstName = "Ali",
            LastName = "Aliyev",
            PassportNumber = "A1234567",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1)
        };
        context.Passengers.Add(passenger);

        var ticket = new Ticket
        {
            TicketNumber = "ET-TEST-001",
            Flight = f1,
            Booking = booking,
            Passenger = passenger,
            Seat = s1,
            Price = 150m,
            IsCancelled = false
        };
        context.Tickets.Add(ticket);

        // Generate 12 more Scheduled flights on Aircraft b738 for pagination testing (Total 14 eligible flights: f1, f2 + 12 = 14)
        for (int i = 4; i <= 15; i++)
        {
            context.Flights.Add(new Flight
            {
                FlightNumber = $"J2-{100 + i}",
                Airline = azal,
                Aircraft = b738,
                DepartureAirport = gyd,
                ArrivalAirport = ist,
                DepartureTimeUtc = depTime.AddMinutes(i * 10),
                ArrivalTimeUtc = arrTime.AddMinutes(i * 10),
                BasePrice = 150m + i,
                Stops = 0,
                Status = FlightStatus.Scheduled
            });
        }

        await context.SaveChangesAsync();

        TestFlight1Id = f1.Id;
        TestFlight2DelayedId = f2.Id;
        TestFlight3CancelledId = f3.Id;
        TestDepartureAirportId = gyd.Id;
        TestArrivalAirportId = ist.Id;
    }

    private sealed class FixedTimeProvider(DateTimeOffset fixedUtcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => fixedUtcNow;
    }
}
