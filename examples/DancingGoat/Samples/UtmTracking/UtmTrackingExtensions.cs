using System;
using System.Linq;

using Kentico.OnlineMarketing.Web.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.DancingGoat;

/// <summary>
/// Registers UTM tracking services.
/// </summary>
public static class UtmTrackingExtensions
{
    /// <summary>
    /// Registers the request-scoped <see cref="UtmParameters"/> and decorates <see cref="IWebPagesActivityLogger"/> with <see cref="UtmWebPagesActivityLogger"/>.
    /// </summary>
    /// <remarks>
    /// Call after <c>AddKentico</c>, which adds the system <see cref="IWebPagesActivityLogger"/> registration this method decorates.
    /// </remarks>
    public static IServiceCollection AddUtmTracking(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<UtmParameters>();

        DecorateWebPagesActivityLogger(services);

        return services;
    }


    private static void DecorateWebPagesActivityLogger(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(service => service.ServiceType == typeof(IWebPagesActivityLogger))
            ?? throw new InvalidOperationException($"No {nameof(IWebPagesActivityLogger)} registration found. Call {nameof(AddUtmTracking)} after AddKentico.");

        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(IWebPagesActivityLogger),
            serviceProvider => new UtmWebPagesActivityLogger(CreateInstance(serviceProvider, descriptor), serviceProvider.GetRequiredService<IHttpContextAccessor>()),
            descriptor.Lifetime));
    }


    private static IWebPagesActivityLogger CreateInstance(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return (IWebPagesActivityLogger)descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IWebPagesActivityLogger)descriptor.ImplementationFactory(serviceProvider);
        }

        return (IWebPagesActivityLogger)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
    }
}
