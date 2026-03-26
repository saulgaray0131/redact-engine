using NetArchTest.Rules;
using RedactEngine.Domain.Common;

namespace RedactEngine.Architecture.Tests;

/// <summary>
/// Enforces domain modeling conventions from copilot-instructions.md:
///
/// - Domain events must inherit from DomainEvent (which implements IDomainEvent + INotification).
/// - Entities must inherit from the Entity base class.
/// - Domain must not reference infrastructure or external service concerns.
/// - Preserve the domain-event + outbox pattern for transactional event capture.
/// </summary>
public class DomainConventionTests
{
    [Fact]
    public void DomainEvents_ShouldInheritFrom_DomainEventBaseClass()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .That()
            .ResideInNamespace("RedactEngine.Domain.Events")
            .And()
            .AreClasses()
            .Should()
            .Inherit(typeof(DomainEvent))
            .GetResult();

        Assert.True(result.IsSuccessful,
            "All domain event classes in RedactEngine.Domain.Events must inherit from DomainEvent. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Entities_ShouldInheritFrom_EntityBaseClass()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .That()
            .ResideInNamespace("RedactEngine.Domain.Entities")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .Inherit(typeof(Entity))
            .GetResult();

        Assert.True(result.IsSuccessful,
            "All concrete classes in RedactEngine.Domain.Entities must inherit from Entity. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Domain_ShouldNotReference_AspNetCore()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must not reference ASP.NET Core. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Domain_ShouldNotReference_DependencyInjection()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.Extensions.DependencyInjection")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must not reference DI framework. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Domain_ShouldNotReference_Dapr()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Dapr", "Dapr.Client", "Dapr.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must not reference Dapr. Infrastructure concerns must stay in Infrastructure. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Domain_ShouldNotReference_Logging()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.Extensions.Logging",
                "Serilog",
                "NLog")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must not reference logging frameworks. " +
            $"Violating types: {FormatViolators(result)}");
    }

    private static string FormatViolators(TestResult result)
    {
        var names = result.FailingTypes?.Select(t => t.FullName) ?? [];
        return string.Join(", ", names);
    }
}
