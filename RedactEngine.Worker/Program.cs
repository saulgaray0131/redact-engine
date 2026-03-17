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

var app = builder.Build();

app.UseCloudEvents();

app.MapSubscribeHandler();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive");

app.Run();
