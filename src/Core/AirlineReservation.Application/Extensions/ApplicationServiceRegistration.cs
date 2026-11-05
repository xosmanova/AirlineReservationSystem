using System.Reflection;
using AirlineReservation.Application.Common.Mappings;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineReservation.Application.Extensions;

/// <summary>
/// Service registration extensions for Application layer dependencies (MediatR, Mapperly).
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register MediatR handlers from the Application assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        // Register high-performance Riok.Mapperly mapper as a singleton
        services.AddSingleton<AirlineMappingProfile>();

        return services;
    }
}
