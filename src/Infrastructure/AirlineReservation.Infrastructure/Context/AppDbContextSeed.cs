using System.Diagnostics;
using AirlineReservation.Application.Common.Generators;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Domain.Enums;
using AirlineReservation.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AirlineReservation.Infrastructure.Context;

/// <summary>
/// Aviation and Identity database seed engine.
/// All data generated is FICTIONAL / DEMO data intended solely for testing, demonstration, and validation.
/// </summary>
public static class AppDbContextSeed
{
    // GPS Coordinates for demo hub and destination airports
    private static readonly Dictionary<string, (double Lat, double Lon)> AirportCoordinatesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GYD"] = (40.4675, 50.0467), // Heydar Aliyev International Airport (Baku, Hub)
        ["IST"] = (41.2753, 28.7519), // Istanbul Airport (Hub)
        ["SAW"] = (40.8986, 29.3092), // Sabiha Gokcen International Airport (Istanbul)
        ["DXB"] = (25.2532, 55.3657), // Dubai International Airport (Hub)
        ["JFK"] = (40.6413, -73.7781), // John F. Kennedy International Airport (New York)
        ["LHR"] = (51.4700, -0.4543), // Heathrow Airport (London)
        ["FRA"] = (50.0379, 8.5622)   // Frankfurt Airport (Hub)
    };

    public static async Task SeedAsync(
        AppDbContext context,
        IConfiguration? configuration = null,
        ILogger? logger = null)
    {
        // 0. Configuration checks: Seed:Enabled
        bool seedEnabled = true;
        if (configuration?["Seed:Enabled"] is { } enabledStr && bool.TryParse(enabledStr, out var parsedEnabled))
        {
            seedEnabled = parsedEnabled;
        }

        if (!seedEnabled)
        {
            logger?.LogInformation("Database seeding is disabled via configuration (Seed:Enabled=false).");
            return;
        }

        int flightDays = 30;
        if (configuration?["Seed:FlightDays"] is { } daysStr && int.TryParse(daysStr, out var parsedDays))
        {
            flightDays = parsedDays;
        }
        var stopwatch = Stopwatch.StartNew();

        // 1. Seed Airports
        if (!await context.Airports.AnyAsync())
        {
            var airports = new List<Airport>
            {
                new() { IataCode = "GYD", Name = "Heydar Aliyev International Airport", City = "Baku", Country = "Azerbaijan", TimeZone = "UTC+4", Aliases = "Baku,Bakı,Baki,Баку" },
                new() { IataCode = "IST", Name = "Istanbul Airport", City = "Istanbul", Country = "Turkey", TimeZone = "UTC+3", Aliases = "Istanbul,İstanbul,Istanbul Airport,Стамбул" },
                new() { IataCode = "SAW", Name = "Sabiha Gokcen International Airport", City = "Istanbul", Country = "Turkey", TimeZone = "UTC+3", Aliases = "Istanbul,İstanbul,Sabiha Gökçen,Sabiha Gokcen,Стамбул" },
                new() { IataCode = "DXB", Name = "Dubai International Airport", City = "Dubai", Country = "United Arab Emirates", TimeZone = "UTC+4", Aliases = "Dubai,Dubay,Дубай" },
                new() { IataCode = "JFK", Name = "John F. Kennedy International Airport", City = "New York", Country = "United States", TimeZone = "UTC-5", Aliases = "New York,Nyu-York,Нью-Йорк" },
                new() { IataCode = "LHR", Name = "Heathrow Airport", City = "London", Country = "United Kingdom", TimeZone = "UTC+0", Aliases = "London,Londan,Лондон" },
                new() { IataCode = "FRA", Name = "Frankfurt Airport", City = "Frankfurt", Country = "Germany", TimeZone = "UTC+1", Aliases = "Frankfurt,Франкфурт" }
            };

            await context.Airports.AddRangeAsync(airports);
            await context.SaveChangesAsync();
        }
        else
        {
            var airportsWithoutAliases = await context.Airports.Where(a => string.IsNullOrEmpty(a.Aliases)).ToListAsync();
            if (airportsWithoutAliases.Count > 0)
            {
                var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GYD"] = "Baku,Bakı,Baki,Баку",
                    ["IST"] = "Istanbul,İstanbul,Istanbul Airport,Стамбул",
                    ["SAW"] = "Istanbul,İstanbul,Sabiha Gökçen,Sabiha Gokcen,Стамбул",
                    ["DXB"] = "Dubai,Dubay,Дубай",
                    ["JFK"] = "New York,Nyu-York,Нью-Йорк",
                    ["LHR"] = "London,Londan,Лондон",
                    ["FRA"] = "Frankfurt,Франкфурт"
                };
                foreach (var a in airportsWithoutAliases)
                {
                    if (aliasMap.TryGetValue(a.IataCode, out var val))
                    {
                        a.Aliases = val;
                    }
                }
                await context.SaveChangesAsync();
            }
        }

        // 2. Seed Airlines (All 4 airlines)
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

        // 3. Seed Aircrafts (8 fictional DEMO aircraft across hubs) & Seats
        if (!await context.Aircrafts.AnyAsync())
        {
            var azal = await context.Airlines.FirstAsync(a => a.Code == "J2");
            var thy = await context.Airlines.FirstAsync(a => a.Code == "TK");
            var emirates = await context.Airlines.FirstAsync(a => a.Code == "EK");
            var lufthansa = await context.Airlines.FirstAsync(a => a.Code == "LH");

            // Fictional tail numbers DEMO-xxx (8 aircraft)
            // Real seat count is the single source of truth (derived from pure SeatLayoutGenerator)
            var aircraftDefinitions = new[]
            {
                // J2 A320-200 x2 (GYD hub), 25 rows
                (Model: "Airbus A320-200", TailNumber: "DEMO-A320-1", AirlineId: azal.Id, Rows: 25, Layout: AircraftLayoutType.NarrowBody),
                (Model: "Airbus A320-200", TailNumber: "DEMO-A320-2", AirlineId: azal.Id, Rows: 25, Layout: AircraftLayoutType.NarrowBody),

                // TK 737-800 x2 (IST hub / GYD hub), 27 rows
                (Model: "Boeing 737-800", TailNumber: "DEMO-B738-1", AirlineId: thy.Id, Rows: 27, Layout: AircraftLayoutType.NarrowBody),
                (Model: "Boeing 737-800", TailNumber: "DEMO-B738-2", AirlineId: thy.Id, Rows: 27, Layout: AircraftLayoutType.NarrowBody),

                // EK 777-300ER x2 (DXB hub), 30 rows (3-4-3 economy)
                (Model: "Boeing 777-300ER", TailNumber: "DEMO-B773-1", AirlineId: emirates.Id, Rows: 30, Layout: AircraftLayoutType.WideBody),
                (Model: "Boeing 777-300ER", TailNumber: "DEMO-B773-2", AirlineId: emirates.Id, Rows: 30, Layout: AircraftLayoutType.WideBody),

                // LH A321 x2 (FRA hub), 30 rows
                (Model: "Airbus A321-200", TailNumber: "DEMO-A321-1", AirlineId: lufthansa.Id, Rows: 30, Layout: AircraftLayoutType.NarrowBody),
                (Model: "Airbus A321-200", TailNumber: "DEMO-A321-2", AirlineId: lufthansa.Id, Rows: 30, Layout: AircraftLayoutType.NarrowBody)
            };

            var aircrafts = new List<Aircraft>();
            var allSeats = new List<Seat>();

            foreach (var def in aircraftDefinitions)
            {
                var seatDescriptors = SeatLayoutGenerator.Generate(def.Rows, def.Layout, businessRows: 4);

                var aircraft = new Aircraft
                {
                    Model = def.Model,
                    TailNumber = def.TailNumber,
                    TotalSeats = seatDescriptors.Count, // Source of truth: matches generated seats exactly
                    AirlineId = def.AirlineId
                };

                aircrafts.Add(aircraft);
            }

            await context.Aircrafts.AddRangeAsync(aircrafts);
            await context.SaveChangesAsync(); // Persist aircrafts so they have IDs for Seats

            // Generate Seats for each aircraft
            for (int i = 0; i < aircraftDefinitions.Length; i++)
            {
                var def = aircraftDefinitions[i];
                var aircraft = aircrafts[i];
                var seatDescriptors = SeatLayoutGenerator.Generate(def.Rows, def.Layout, businessRows: 4);

                foreach (var desc in seatDescriptors)
                {
                    allSeats.Add(new Seat
                    {
                        AircraftId = aircraft.Id,
                        SeatNumber = desc.SeatNumber,
                        RowNumber = desc.RowNumber,
                        ColumnLetter = desc.ColumnLetter,
                        SeatClass = desc.SeatClass,
                        BasePriceMultiplier = desc.BasePriceMultiplier,
                        IsWindow = desc.IsWindow,
                        IsAisle = desc.IsAisle,
                        IsExitRow = desc.IsExitRow
                    });
                }
            }

            await context.Seats.AddRangeAsync(allSeats);
            await context.SaveChangesAsync();
        }

        // 4. Seed Flights using FlightScheduleGenerator (all 8 aircraft, next 30 days)
        if (!await context.Flights.AnyAsync())
        {
            var airports = await context.Airports.ToDictionaryAsync(a => a.IataCode, StringComparer.OrdinalIgnoreCase);
            var airlines = await context.Airlines.ToDictionaryAsync(a => a.Code, StringComparer.OrdinalIgnoreCase);
            var aircrafts = await context.Aircrafts.ToDictionaryAsync(a => a.TailNumber, StringComparer.OrdinalIgnoreCase);

            AirportCoordinates ToCoordinates(Airport ap)
            {
                var (lat, lon) = AirportCoordinatesMap[ap.IataCode];
                return new AirportCoordinates(ap.Id, ap.IataCode, lat, lon);
            }

            var gyd = ToCoordinates(airports["GYD"]);
            var ist = ToCoordinates(airports["IST"]);
            var saw = ToCoordinates(airports["SAW"]);
            var dxb = ToCoordinates(airports["DXB"]);
            var jfk = ToCoordinates(airports["JFK"]);
            var lhr = ToCoordinates(airports["LHR"]);
            var fra = ToCoordinates(airports["FRA"]);

            var startDateUtc = DateTime.UtcNow.Date;

            // Define schedule requests for all 8 aircraft
            var scheduleRequests = new List<AircraftScheduleRequest>
            {
                // J2 A320-200 #1, hub GYD: routes GYD-IST, GYD-SAW, GYD-DXB
                new(
                    AircraftId: aircrafts["DEMO-A320-1"].Id,
                    AirlineId: airlines["J2"].Id,
                    AirlineCode: "J2",
                    HubAirport: gyd,
                    DestinationAirports: [ist, saw, dxb],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    BaseFlightNumber: 101
                ),
                // J2 A320-200 #2, hub GYD: offset rotation
                new(
                    AircraftId: aircrafts["DEMO-A320-2"].Id,
                    AirlineId: airlines["J2"].Id,
                    AirlineCode: "J2",
                    HubAirport: gyd,
                    DestinationAirports: [saw, dxb, ist],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    InitialDepartureOffset: TimeSpan.FromHours(3),
                    BaseFlightNumber: 121
                ),

                // TK 737-800 #1, hub IST: routes IST-GYD, IST-LHR, IST-FRA, IST-DXB
                new(
                    AircraftId: aircrafts["DEMO-B738-1"].Id,
                    AirlineId: airlines["TK"].Id,
                    AirlineCode: "TK",
                    HubAirport: ist,
                    DestinationAirports: [gyd, lhr, fra, dxb],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    BaseFlightNumber: 201
                ),
                // TK 737-800 #2, hub GYD: GYD-LHR with Stops=1 (duration +120m, price 10% lower)
                new(
                    AircraftId: aircrafts["DEMO-B738-2"].Id,
                    AirlineId: airlines["TK"].Id,
                    AirlineCode: "TK",
                    HubAirport: gyd,
                    DestinationAirports: [lhr],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    Stops: 1,
                    InitialDepartureOffset: TimeSpan.FromHours(1),
                    BaseFlightNumber: 231
                ),

                // EK 777-300ER #1, hub DXB: routes DXB-JFK, DXB-LHR, DXB-IST, DXB-GYD
                new(
                    AircraftId: aircrafts["DEMO-B773-1"].Id,
                    AirlineId: airlines["EK"].Id,
                    AirlineCode: "EK",
                    HubAirport: dxb,
                    DestinationAirports: [jfk, lhr, ist, gyd],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    BaseFlightNumber: 301
                ),
                // EK 777-300ER #2, hub DXB: offset rotation
                new(
                    AircraftId: aircrafts["DEMO-B773-2"].Id,
                    AirlineId: airlines["EK"].Id,
                    AirlineCode: "EK",
                    HubAirport: dxb,
                    DestinationAirports: [lhr, ist, gyd, jfk],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    InitialDepartureOffset: TimeSpan.FromHours(4),
                    BaseFlightNumber: 331
                ),

                // LH A321 #1, hub FRA: routes FRA-LHR, FRA-IST, FRA-GYD
                new(
                    AircraftId: aircrafts["DEMO-A321-1"].Id,
                    AirlineId: airlines["LH"].Id,
                    AirlineCode: "LH",
                    HubAirport: fra,
                    DestinationAirports: [lhr, ist, gyd],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    BaseFlightNumber: 401
                ),
                // LH A321 #2, hub FRA: offset rotation
                new(
                    AircraftId: aircrafts["DEMO-A321-2"].Id,
                    AirlineId: airlines["LH"].Id,
                    AirlineCode: "LH",
                    HubAirport: fra,
                    DestinationAirports: [ist, gyd, lhr],
                    StartDateUtc: startDateUtc,
                    TotalDays: flightDays,
                    InitialDepartureOffset: TimeSpan.FromHours(2),
                    BaseFlightNumber: 421
                )
            };

            var dbFlights = new List<Flight>();

            foreach (var req in scheduleRequests)
            {
                var generatedFlights = FlightScheduleGenerator.GenerateScheduleForAircraft(req);

                foreach (var gf in generatedFlights)
                {
                    dbFlights.Add(new Flight
                    {
                        FlightNumber = gf.FlightNumber,
                        AirlineId = gf.AirlineId,
                        AircraftId = gf.AircraftId,
                        DepartureAirportId = gf.DepartureAirportId,
                        ArrivalAirportId = gf.ArrivalAirportId,
                        DepartureTimeUtc = gf.DepartureTimeUtc,
                        ArrivalTimeUtc = gf.ArrivalTimeUtc,
                        BasePrice = gf.BasePrice,
                        Stops = gf.Stops,
                        Status = FlightStatus.Scheduled
                    });
                }
            }

            await context.Flights.AddRangeAsync(dbFlights);
            await context.SaveChangesAsync();
        }

        stopwatch.Stop();

        // 5. Query and log totals & elapsed time
        int totalAirports = await context.Airports.CountAsync();
        int totalAirlines = await context.Airlines.CountAsync();
        int totalAircrafts = await context.Aircrafts.CountAsync();
        int totalSeats = await context.Seats.CountAsync();
        int totalFlights = await context.Flights.CountAsync();

        string summary = $"[SEED COMPLETE] Airports: {totalAirports}, Airlines: {totalAirlines}, Aircrafts: {totalAircrafts}, Seats: {totalSeats}, Flights: {totalFlights} | Elapsed: {stopwatch.ElapsedMilliseconds} ms.";
        logger?.LogInformation("{Summary}", summary);
        Console.WriteLine(summary);
    }

    /// <summary>
    /// Seeds Identity roles (Admin, Customer) and the initial Admin user.
    /// Admin password must be provided strictly via configuration (Seed:AdminPassword), typically via user-secrets in development.
    /// If Seed:AdminPassword is missing, admin user creation is safely skipped without throwing an exception.
    /// </summary>
    public static async Task SeedIdentityAsync(
        IServiceProvider serviceProvider,
        IConfiguration? configuration = null,
        ILogger? logger = null)
    {
        bool seedEnabled = true;
        if (configuration?["Seed:Enabled"] is { } enabledStr && bool.TryParse(enabledStr, out var parsedEnabled))
        {
            seedEnabled = parsedEnabled;
        }

        if (!seedEnabled)
        {
            logger?.LogInformation("Identity seeding is disabled via configuration (Seed:Enabled=false).");
            return;
        }

        var roleManager = serviceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

        await SeedIdentityCoreAsync(roleManager, userManager, configuration, logger);
    }

    /// <summary>
    /// Core seeding logic decoupled from IServiceProvider for high unit testability.
    /// </summary>
    public static async Task SeedIdentityCoreAsync(
        RoleManager<AppRole> roleManager,
        UserManager<AppUser> userManager,
        IConfiguration? configuration = null,
        ILogger? logger = null)
    {
        bool seedEnabled = true;
        if (configuration?["Seed:Enabled"] is { } enabledStr && bool.TryParse(enabledStr, out var parsedEnabled))
        {
            seedEnabled = parsedEnabled;
        }

        if (!seedEnabled)
        {
            return;
        }

        // 1. Seed Roles: Admin, Customer
        string[] roles = ["Admin", "Customer"];
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new AppRole(roleName));
                if (roleResult.Succeeded)
                {
                    logger?.LogInformation("Role {RoleName} seeded successfully.", roleName);
                }
                else
                {
                    var errorCodes = string.Join(", ", roleResult.Errors.Select(e => e.Code));
                    logger?.LogError("Failed to seed role {RoleName}: {ErrorCodes}", roleName, errorCodes);
                }
            }
        }

        // 2. Read Admin configuration (Seed:AdminPassword only from configuration/user secrets)
        string adminEmail = string.IsNullOrWhiteSpace(configuration?["Seed:AdminEmail"])
            ? "admin@airline.test"
            : configuration["Seed:AdminEmail"]!;
        string? adminPassword = configuration?["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger?.LogWarning("Admin password configuration (Seed:AdminPassword) is missing or empty. Skipping admin user creation.");
            Console.WriteLine("[IDENTITY SEED] Warning: Seed:AdminPassword is missing or empty. Skipping admin user creation.");
            return;
        }

        // 3. Seed Admin user if not already present
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Administrator",
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger?.LogInformation("Admin user {AdminEmail} seeded successfully with Admin role.", adminEmail);
                Console.WriteLine($"[IDENTITY SEED] Admin user '{adminEmail}' seeded successfully.");
            }
            else
            {
                var errorCodes = string.Join(", ", createResult.Errors.Select(e => e.Code));
                logger?.LogWarning("Failed to seed admin user {AdminEmail}. Validation error codes: {ErrorCodes}. Skipping.", adminEmail, errorCodes);
                Console.WriteLine($"[IDENTITY SEED WARNING] Failed to seed admin user ({errorCodes}). Skipping.");
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            {
                await userManager.AddToRoleAsync(existingAdmin, "Admin");
                logger?.LogInformation("Admin user {AdminEmail} already existed; added missing Admin role.", adminEmail);
            }
        }
    }
}
