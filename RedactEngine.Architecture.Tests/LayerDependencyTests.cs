using NetArchTest.Rules;

namespace RedactEngine.Architecture.Tests;

/// <summary>
/// Enforces layer dependency rules per DDD architecture guidelines.
///
/// Rules from copilot-instructions.md:
/// - Domain: business rules, invariants, domain events. No outward dependencies.
/// - Application: orchestration, validation, service contracts. Depends only on Domain.
/// - Infrastructure: persistence, external integrations. Depends on Domain + Application.
/// - ApiService &amp; Worker: thin entry points. Must not reference each other.
/// - Shared: cross-service contracts only. No inward dependencies on Domain/Application/Infrastructure.
/// - ServiceDefaults: hosting, telemetry, resilience. No inward dependencies on Domain/Application/Infrastructure.
/// - AppHost: orchestration only, not tested here (it references everything by design for Aspire wiring).
/// </summary>
public class LayerDependencyTests
{
    private const string DomainNamespace = "RedactEngine.Domain";
    private const string ApplicationNamespace = "RedactEngine.Application";
    private const string InfrastructureNamespace = "RedactEngine.Infrastructure";
    private const string ApiServiceNamespace = "RedactEngine.ApiService";
    private const string WorkerNamespace = "RedactEngine.Worker";
    private const string SharedNamespace = "RedactEngine.Shared";
    private const string ServiceDefaultsNamespace = "RedactEngine.ServiceDefaults";

    // ── Domain layer: must not depend on any other project layer ──

    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Domain", "Application", result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Domain", "Infrastructure", result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_ApiService()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(ApiServiceNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Domain", "ApiService", result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Worker()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(WorkerNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Domain", "Worker", result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Shared()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(SharedNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Domain", "Shared", result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_ServiceDefaults()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceDefaultsNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Domain", "ServiceDefaults", result));
    }

    // ── Application layer: depends on Domain only ──

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(SolutionAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Application", "Infrastructure", result));
    }

    [Fact]
    public void Application_ShouldNotDependOn_ApiService()
    {
        var result = Types.InAssembly(SolutionAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny(ApiServiceNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Application", "ApiService", result));
    }

    [Fact]
    public void Application_ShouldNotDependOn_Worker()
    {
        var result = Types.InAssembly(SolutionAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny(WorkerNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Application", "Worker", result));
    }

    // ── Infrastructure layer: depends on Domain + Application only ──

    [Fact]
    public void Infrastructure_ShouldNotDependOn_ApiService()
    {
        var result = Types.InAssembly(SolutionAssemblies.Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(ApiServiceNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Infrastructure", "ApiService", result));
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Worker()
    {
        var result = Types.InAssembly(SolutionAssemblies.Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(WorkerNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Infrastructure", "Worker", result));
    }

    // ── ApiService and Worker must not reference each other ──

    [Fact]
    public void ApiService_ShouldNotDependOn_Worker()
    {
        var result = Types.InAssembly(SolutionAssemblies.ApiService)
            .ShouldNot()
            .HaveDependencyOnAny(WorkerNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("ApiService", "Worker", result));
    }

    [Fact]
    public void Worker_ShouldNotDependOn_ApiService()
    {
        var result = Types.InAssembly(SolutionAssemblies.Worker)
            .ShouldNot()
            .HaveDependencyOnAny(ApiServiceNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Worker", "ApiService", result));
    }

    // ── Shared: cross-service contracts only, no inward deps ──

    [Fact]
    public void Shared_ShouldNotDependOn_Domain()
    {
        var result = Types.InAssembly(SolutionAssemblies.Shared)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Shared", "Domain", result));
    }

    [Fact]
    public void Shared_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(SolutionAssemblies.Shared)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Shared", "Application", result));
    }

    [Fact]
    public void Shared_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(SolutionAssemblies.Shared)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("Shared", "Infrastructure", result));
    }

    // ── ServiceDefaults: hosting only, no business deps ──

    [Fact]
    public void ServiceDefaults_ShouldNotDependOn_Domain()
    {
        var result = Types.InAssembly(SolutionAssemblies.ServiceDefaults)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("ServiceDefaults", "Domain", result));
    }

    [Fact]
    public void ServiceDefaults_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(SolutionAssemblies.ServiceDefaults)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("ServiceDefaults", "Application", result));
    }

    [Fact]
    public void ServiceDefaults_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(SolutionAssemblies.ServiceDefaults)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage("ServiceDefaults", "Infrastructure", result));
    }

    private static string FailMessage(string source, string forbidden, TestResult result)
    {
        var violators = result.FailingTypes?.Select(t => t.FullName) ?? [];
        return $"{source} must not depend on {forbidden}. " +
               $"Violating types: {string.Join(", ", violators)}";
    }
}
