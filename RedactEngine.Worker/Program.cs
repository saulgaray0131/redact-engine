using Dapr.Client;
using RedactEngine.Application;
using RedactEngine.Infrastructure;
using RedactEngine.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<ApplicationDbContext>("Core",
    configureSettings: settings => settings.DisableTracing = true);
builder.Services.AddApplication();
builder.Services.AddSingleton<DaprClient>(_ => new DaprClientBuilder().Build());
builder.Services.AddControllers().AddDapr();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

builder.Services.AddHttpClient("InferenceService", client =>
{
    var inferenceUrl = builder.Configuration.GetConnectionString("InferenceService")
                      ?? "http://localhost:8000";
    client.BaseAddress = new Uri(inferenceUrl);
    // /redact is now async: this client only submits detect requests and
    // fire-and-forget POSTs to /redact. Detect still holds the connection for
    // the full DINO pass (tens of seconds), so allow up to a minute to absorb
    // that plus any cross-region ingress hop; /redact itself returns in ms.
    client.Timeout = TimeSpan.FromMinutes(1);

    // In prod the inference service lives in a separate ACA environment (eastus)
    // and is reached over a public FQDN, so gate access with a shared secret.
    // Unset in local Aspire, where the service is loopback-only.
    var inferenceKey = builder.Configuration["InferenceService:ApiKey"]
                      ?? Environment.GetEnvironmentVariable("INFERENCE_SERVICE_KEY");
    if (!string.IsNullOrEmpty(inferenceKey))
    {
        client.DefaultRequestHeaders.Add("X-Inference-Key", inferenceKey);
    }
});

var app = builder.Build();

app.UseCloudEvents();

app.MapSubscribeHandler();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive");

app.Run();
