using NetArchTest.Rules;

namespace RedactEngine.Architecture.Tests;

/// <summary>
/// Enforces infrastructure and entry-point conventions from copilot-instructions.md:
///
/// - Keep entity configuration, migrations, and DB-specific concerns in Infrastructure.
/// - ApiService controllers and Worker processes must be thin.
/// - Shared is only for cross-service contracts.
/// - ServiceDefaults is only for hosting, telemetry, health, and resilience setup.
/// </summary>
public class InfrastructureConventionTests
{
    [Fact]
    public void ApiService_Controllers_ShouldResideInControllersNamespace()
    {
        var result = Types.InAssembly(SolutionAssemblies.ApiService)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .ResideInNamespaceContaining("Controllers")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "All ApiService controllers must reside in a Controllers namespace. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Worker_Controllers_ShouldResideInControllersNamespace()
    {
        var result = Types.InAssembly(SolutionAssemblies.Worker)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .ResideInNamespaceContaining("Controllers")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "All Worker controllers must reside in a Controllers namespace. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void ApiService_ShouldNotContain_DbContextTypes()
    {
        var dbContextTypes = Types.InAssembly(SolutionAssemblies.ApiService)
            .That()
            .HaveNameEndingWith("DbContext")
            .GetTypes();

        Assert.True(!dbContextTypes.Any(),
            "ApiService must not define DbContext types. Keep persistence in Infrastructure.");
    }

    [Fact]
    public void Worker_ShouldNotContain_DbContextTypes()
    {
        var dbContextTypes = Types.InAssembly(SolutionAssemblies.Worker)
            .That()
            .HaveNameEndingWith("DbContext")
            .GetTypes();

        Assert.True(!dbContextTypes.Any(),
            "Worker must not define DbContext types. Keep persistence in Infrastructure.");
    }

    [Fact]
    public void ApiService_ShouldNotContain_EntityConfigurations()
    {
        var configTypes = Types.InAssembly(SolutionAssemblies.ApiService)
            .That()
            .ImplementInterface(typeof(Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<>))
            .GetTypes();

        Assert.True(!configTypes.Any(),
            "ApiService must not define EF entity configurations. Keep them in Infrastructure.");
    }

    [Fact]
    public void Worker_ShouldNotContain_EntityConfigurations()
    {
        var configTypes = Types.InAssembly(SolutionAssemblies.Worker)
            .That()
            .ImplementInterface(typeof(Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<>))
            .GetTypes();

        Assert.True(!configTypes.Any(),
            "Worker must not define EF entity configurations. Keep them in Infrastructure.");
    }

    [Fact]
    public void Shared_ShouldNotContain_Services()
    {
        // Shared is for contracts only — no service implementations.
        var serviceTypes = Types.InAssembly(SolutionAssemblies.Shared)
            .That()
            .HaveNameEndingWith("Service")
            .And()
            .AreClasses()
            .GetTypes();

        Assert.True(!serviceTypes.Any(),
            "Shared must not contain service implementations. It is for cross-service contracts only.");
    }

    [Fact]
    public void Shared_ShouldNotReference_EntityFramework()
    {
        var result = Types.InAssembly(SolutionAssemblies.Shared)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Shared must not reference Entity Framework Core. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void ServiceDefaults_ShouldNotContain_BusinessServices()
    {
        // ServiceDefaults may have extension methods but no business services.
        var serviceClasses = Types.InAssembly(SolutionAssemblies.ServiceDefaults)
            .That()
            .HaveNameEndingWith("Service")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes();

        Assert.True(!serviceClasses.Any(),
            "ServiceDefaults must not contain business service classes. " +
            "It is for shared hosting, telemetry, health, and resilience setup only.");
    }

    private static string FormatViolators(TestResult result)
    {
        var names = result.FailingTypes?.Select(t => t.FullName) ?? [];
        return string.Join(", ", names);
    }
}
