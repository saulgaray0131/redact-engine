using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedactEngine.Application.Common;
using RedactEngine.Application.Common.Interfaces;
using RedactEngine.Domain.Repositories;
using RedactEngine.Infrastructure.Persistence;
using RedactEngine.Infrastructure.Services;

namespace RedactEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var blobConnectionString = configuration.GetConnectionString("BlobStorage")
            ?? "UseDevelopmentStorage=true";

        services.AddSingleton(_ => new BlobServiceClient(blobConnectionString));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IBlobService, AzureBlobService>();

        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        var llmMode = configuration[$"{LlmOptions.SectionName}:Mode"] ?? "mock";
        if (string.Equals(llmMode, "azure-openai", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ILlmPromptTranslator, AzureOpenAiPromptTranslator>();
        }
        else
        {
            services.AddScoped<ILlmPromptTranslator, MockPromptTranslator>();
        }

        return services;
    }
}
