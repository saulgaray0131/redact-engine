using RedactEngine.AppHost.Infrastructure.Configuration;

namespace RedactEngine.AppHost.Infrastructure.Database;

public static class PostgresExtensions
{
    public static (IResourceBuilder<PostgresServerResource> postgres, IResourceBuilder<PostgresDatabaseResource> database)
        AddRedactEnginePostgres(
            this IDistributedApplicationBuilder builder,
            InfrastructureOptions options)
    {
        var postgresPassword = builder.AddParameter("postgres-password");
        var postgres = builder.AddPostgres("postgres")
            .WithPassword(postgresPassword)
            .WithLifetime(ContainerLifetime.Persistent)
            .WithHostPort(5432);

        var database = postgres.AddDatabase(options.Postgres.DatabaseName);

        return (postgres, database);
    }
}
