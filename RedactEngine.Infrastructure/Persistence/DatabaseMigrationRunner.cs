using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedactEngine.Application.Common.Interfaces;

namespace RedactEngine.Infrastructure.Persistence;

/// <summary>
/// Runs EF Core migrations for the primary schema with retry logic.
/// </summary>
public static class DatabaseMigrationRunner
{
    public static async Task<bool> MigrateAsync(
        IServiceProvider serviceProvider,
        ILogger logger,
        bool runSeeders,
        CancellationToken cancellationToken = default)
    {
        const int maxRetries = 5;
        var delay = TimeSpan.FromMilliseconds(500);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var services = scope.ServiceProvider;

                var applicationDbContext = services.GetRequiredService<ApplicationDbContext>();
                await ApplyMigrationsAsync(applicationDbContext, logger, cancellationToken);

                if (runSeeders)
                {
                    logger.LogInformation("Running data seeders...");
                    var seeders = services.GetServices<IDataSeeder>()
                        .OrderBy(seeder => seeder.Order)
                        .ToList();

                    foreach (var seeder in seeders)
                    {
                        logger.LogInformation("Running data seeder {SeederName}...", seeder.Name);
                        await seeder.SeedAsync(cancellationToken);
                    }
                }

                return true;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(
                    ex,
                    "Migration attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}ms...",
                    attempt,
                    maxRetries,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed after {Attempts} attempts", maxRetries);
                return false;
            }
        }

        return false;
    }

    private static async Task ApplyMigrationsAsync<TContext>(
        TContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var contextName = typeof(TContext).Name;

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Could not connect to database for {contextName}");
        }

        logger.LogInformation("Running database migrations for {DbContext}...", contextName);
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations completed successfully for {DbContext}", contextName);
    }
}
