using Microsoft.EntityFrameworkCore;
using RedactEngine.ApiService.Extensions;
using RedactEngine.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<ApplicationDbContext>("Core",
    configureSettings: settings => settings.DisableTracing = true);

builder.Services.AddRedactEngineServices(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddRedactEngineControllers();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();


await app.MigrateAndSeedDatabaseAsync();


app.MapOpenApi();
app.MapScalarApiReference("/scalar").AllowAnonymous();

app.UseRedactEnginePipeline();
app.MapRedactEngineEndpoints();

app.Run();
