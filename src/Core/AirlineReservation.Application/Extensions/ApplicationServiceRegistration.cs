using System.Reflection;
using AirlineReservation.Application.Common.Behaviors;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

using AirlineReservation.Application.Common.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AirlineReservation.Application.Extensions;

/// <summary>
/// Service registration extensions for Application layer dependencies (MediatR, FluentValidation, Mapperly, BusinessTime).
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register default TimeProvider
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new PricingSettings());
        services.TryAddSingleton<IPricingService, PricingService>();

        // Register IBusinessTime for unified timezone-aware day boundary calculations
        services.AddSingleton<IBusinessTime, BusinessTimeService>();

        // Register MediatR handlers and validation pipeline behavior
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Register all FluentValidation validators in the Application assembly
        services.AddValidatorsFromAssembly(assembly);

        // Register high-performance Riok.Mapperly mapper as a singleton
        services.AddSingleton<AirlineMappingProfile>();

        return services;
    }
}
