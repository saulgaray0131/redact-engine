using RedactEngine.AppHost.Infrastructure.Configuration;
using RedactEngine.AppHost.Infrastructure.Database;
using RedactEngine.AppHost.Infrastructure.Inference;
using RedactEngine.AppHost.Infrastructure.Llm;
using RedactEngine.AppHost.Infrastructure.Services;
using RedactEngine.AppHost.Infrastructure.Storage;

var builder = DistributedApplication.CreateBuilder(args);

var infrastructureOptions = EnvironmentSettings.GetInfrastructureOptions();

var (_, database) = builder.AddRedactEnginePostgres(infrastructureOptions);
var (_, blobs) = builder.AddRedactEngineStorage(infrastructureOptions);

var apiService = builder
    .AddRedactEngineApiService("redact-engine-api-service", database, blobs, infrastructureOptions)
    .WithLlmTranslation(builder);

// Inference needs the blob connection (to upload redacted videos) and the API
// endpoint (to POST completion callbacks), so it is declared after both.
var inferenceService = builder.AddRedactEngineInferenceService(blobs, apiService);

builder.AddRedactEngineWorkerService("redact-engine-worker", database, blobs, inferenceService, infrastructureOptions);

builder.AddRedactEngineWeb("redact-engine-web", apiService);

builder.Build().Run();
