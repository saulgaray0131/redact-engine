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
    client.Timeout = TimeSpan.FromMinutes(10); // video processing can be slow
});

var app = builder.Build();

app.UseCloudEvents();

app.MapSubscribeHandler();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive");

app.Run();
