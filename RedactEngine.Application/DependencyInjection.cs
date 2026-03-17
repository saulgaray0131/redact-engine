using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RedactEngine.Application.Common;

namespace RedactEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Result>();
        return services;
    }
}