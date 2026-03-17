using Dapr;
using Dapr.Client;
using RedactEngine.Application;
using RedactEngine.Infrastructure;

namespace RedactEngine.ApiService.Extensions;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all RedactEngine services including Application and Infrastructure layers.
    /// </summary>
    public static IServiceCollection AddRedactEngineServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _ = environment;
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddDaprClient();

        return services;
    }

    public static IServiceCollection AddRedactEngineControllers(this IServiceCollection services)
    {
        services.AddControllers().AddDapr();
        return services;
    }
}
