using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Identity.Entities;
using AirlineReservation.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineReservation.Infrastructure.Extensions;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Configure ASP.NET Core Identity with strict password policy
        services.AddIdentity<AppUser, AppRole>(options =>
        {
            // Password policy: min 8, digit, upper, lower, non-alphanumeric. No lockout tuning yet.
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 1;

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IFlightRepository, FlightRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<ISeatRepository, SeatRepository>();
        services.AddScoped<IAirportRepository, AirportRepository>();
        services.AddScoped<IAirlineRepository, AirlineRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
