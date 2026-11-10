using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.Contracts.Persistence;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Identity.Entities;
using AirlineReservation.Infrastructure.Identity.Services;
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

        // Configure ASP.NET Core Identity Core (no cookie authentication schemes for stateless JWT API)
        services.AddIdentityCore<AppUser>(options =>
        {
            // Password policy: min 8, digit, upper, lower, non-alphanumeric
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 1;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<AppRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // Bind JWT configuration (signing key supplied strictly via user-secrets or environment)
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, IdentityAuthService>();

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
