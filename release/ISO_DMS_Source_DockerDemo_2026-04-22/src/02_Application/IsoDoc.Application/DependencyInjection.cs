using System.Reflection;
using AutoMapper;
using FluentValidation;
using IsoDoc.Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IsoDoc.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg =>
        {
            var key = Environment.GetEnvironmentVariable("AUTOMAPPER_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(key))
            {
                cfg.LicenseKey = key;
            }
        }, typeof(DependencyInjection));

        return services;
    }
}

