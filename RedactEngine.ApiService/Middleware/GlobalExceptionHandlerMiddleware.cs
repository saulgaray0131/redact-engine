using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace RedactEngine.ApiService.Middleware;

/// <summary>
/// Global exception handler middleware that catches unhandled exceptions
/// and returns standardized ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        _logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}, Method: {Method}",
            traceId,
            context.Request.Path,
            context.Request.Method);

        var (statusCode, title, detail) = MapExceptionToResponse(exception);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = _environment.IsDevelopment() || _environment.IsEnvironment("Local")
                ? detail
                : "An error occurred while processing your request.",
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId
            }
        };

        if (_environment.IsDevelopment() || _environment.IsEnvironment("Local"))
        {
            problemDetails.Extensions["exception"] = new
            {
                type = exception.GetType().Name,
                message = exception.Message,
                stackTrace = exception.StackTrace?.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            };
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, JsonOptions));
    }

    private static (int StatusCode, string Title, string Detail) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException ex => (
                (int)HttpStatusCode.BadRequest,
                "Invalid Request",
                $"A required parameter was null: {ex.ParamName}"),

            ArgumentException ex => (
                (int)HttpStatusCode.BadRequest,
                "Invalid Request",
                ex.Message),

            InvalidOperationException ex => (
                (int)HttpStatusCode.BadRequest,
                "Invalid Operation",
                ex.Message),

            UnauthorizedAccessException => (
                (int)HttpStatusCode.Unauthorized,
                "Unauthorized",
                "You are not authorized to access this resource."),

            KeyNotFoundException => (
                (int)HttpStatusCode.NotFound,
                "Resource Not Found",
                "The requested resource was not found."),

            NotSupportedException ex => (
                (int)HttpStatusCode.BadRequest,
                "Not Supported",
                ex.Message),

            TimeoutException => (
                (int)HttpStatusCode.GatewayTimeout,
                "Request Timeout",
                "The operation timed out."),

            OperationCanceledException => (
                (int)HttpStatusCode.BadRequest,
                "Request Cancelled",
                "The request was cancelled."),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                "Internal Server Error",
                exception.Message)
        };
    }
}

/// <summary>
/// Extension methods for registering the global exception handler middleware.
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    /// <summary>
    /// Adds the global exception handler middleware to the pipeline.
    /// Should be registered early in the pipeline to catch all exceptions.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
