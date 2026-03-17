namespace RedactEngine.Application.Common.Interfaces;

/// <summary>
/// Interface for data seeding services.
/// Implementations should be idempotent - safe to run multiple times.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Gets the order in which this seeder should run (lower = earlier).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets the name of this seeder for logging purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Seeds data into the database. Must be idempotent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
