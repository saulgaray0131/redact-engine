using Dapr;
using RedactEngine.ApiService.Middleware;
using RedactEngine.Infrastructure.Persistence;

namespace RedactEngine.ApiService.Extensions;

/// <summary>
/// Extension methods for configuring the application middleware pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    public static WebApplication UseRedactEnginePipeline(this WebApplication app)
    {
        app.UseGlobalExceptionHandler();

        app.UseCors(policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());

        if (app.Environment.IsEnvironment("Local"))
        {
            // Skip HTTPS redirect for the inference service's callback path.
            // Python's httpx doesn't trust the ASP.NET dev cert, so bouncing
            // loopback POSTs to https: would break the callback. In prod
            // UseHttpsRedirection is never registered, so this branch is a
            // dev-only concern.
            app.UseWhen(
                ctx => !ctx.Request.Path.StartsWithSegments("/internal"),
                branch => branch.UseHttpsRedirection());
        }

        app.UseCloudEvents();

        return app;
    }

    public static WebApplication MapRedactEngineEndpoints(this WebApplication app)
    {
        app.MapSubscribeHandler();
        app.MapControllers();
        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapHealthChecks("/alive").AllowAnonymous();

        return app;
    }

    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app)
    {
        await DatabaseMigrationRunner.MigrateAsync(
            app.Services,
            app.Logger,
            runSeeders: app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"));
    }
}
