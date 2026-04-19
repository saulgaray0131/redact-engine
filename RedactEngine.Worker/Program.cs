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
    // SAM 2 video tracking is O(frames × memory_bank_size) per-frame. On CPU
    // (no GPU available) a multi-second clip can take 30-60 minutes; on MPS or
    // CUDA it fits within minutes. Size this for the worst case so long-running
    // jobs aren't killed mid-propagation, which leaves the Python process
    // chewing CPU after the worker has already given up.
    client.Timeout = TimeSpan.FromHours(1);
});

var app = builder.Build();

app.UseCloudEvents();

app.MapSubscribeHandler();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive");

app.Run();
