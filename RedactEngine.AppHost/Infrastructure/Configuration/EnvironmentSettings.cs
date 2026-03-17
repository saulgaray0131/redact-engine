namespace RedactEngine.AppHost.Infrastructure.Configuration;

/// <summary>
/// Environment settings for local development infrastructure.
/// Azure deployment is handled by Terraform - this is for local Aspire setup only.
/// </summary>
public static class EnvironmentSettings
{
    public static InfrastructureOptions GetInfrastructureOptions()
    {
        var environment = GetEnvironment();

        return new InfrastructureOptions
        {
            Environment = environment,

            Postgres = new PostgresOptions
            {
                DatabaseName = "Core"
            },

            Storage = new StorageOptions
            {
                Name = "redactenginestoragelocal",
            }
        };
    }

    private static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
               ?? "local";
    }
}