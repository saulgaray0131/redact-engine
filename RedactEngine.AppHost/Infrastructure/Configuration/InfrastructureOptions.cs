namespace RedactEngine.AppHost.Infrastructure.Configuration;

/// <summary>
/// Configuration options for local development infrastructure.
/// Azure deployment is handled by Terraform - this is for local Aspire setup only.
/// </summary>
public class InfrastructureOptions
{
    public required string Environment { get; set; }
    public required PostgresOptions Postgres { get; set; }
    public required StorageOptions Storage { get; set; }
}

public class PostgresOptions
{
    public required string DatabaseName { get; set; }
}

public class StorageOptions
{
    public required string Name { get; set; }
}
