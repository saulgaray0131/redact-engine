using RedactEngine.AppHost.Infrastructure.Configuration;
using RedactEngine.AppHost.Infrastructure.Database;
using RedactEngine.AppHost.Infrastructure.Services;
using RedactEngine.AppHost.Infrastructure.Storage;

var builder = DistributedApplication.CreateBuilder(args);

var infrastructureOptions = EnvironmentSettings.GetInfrastructureOptions();

var (_, database) = builder.AddRedactEnginePostgres(infrastructureOptions);
var (_, blobs) = builder.AddRedactEngineStorage(infrastructureOptions);

builder.AddRedactEngineApiService("redact-engine-api-service", database, blobs, infrastructureOptions);
builder.AddRedactEngineWorkerService("redact-engine-worker", database, blobs, infrastructureOptions);

builder.Build().Run();
