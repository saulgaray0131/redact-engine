using NetArchTest.Rules;

namespace RedactEngine.Architecture.Tests;

/// <summary>
/// Enforces simplicity conventions from copilot-instructions.md:
///
/// - Do NOT introduce CQRS request/handler layers (no IRequest / IRequestHandler).
/// - MediatR is limited to domain-event notification plumbing only (INotification / INotificationHandler).
/// - Do NOT expand MediatR into a general command/query architecture.
/// - No MediatR pipeline behaviors.
/// </summary>
public class SimplicityConventionTests
{
    private static readonly System.Reflection.Assembly[] AllProjectAssemblies =
    [
        SolutionAssemblies.Domain,
        SolutionAssemblies.Application,
        SolutionAssemblies.Infrastructure,
        SolutionAssemblies.ApiService,
        SolutionAssemblies.Worker,
    ];

    // ── No CQRS: IRequest / IRequestHandler must not exist ──

    [Fact]
    public void Solution_ShouldNotContain_MediatRRequestHandlers()
    {
        foreach (var assembly in AllProjectAssemblies)
        {
            var requestHandlerTypes = Types.InAssembly(assembly)
                .That()
                .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
                .GetTypes();

            Assert.True(!requestHandlerTypes.Any(),
                $"Assembly {assembly.GetName().Name} must not contain MediatR IRequestHandler (no CQRS). " +
                $"Found: {string.Join(", ", requestHandlerTypes.Select(t => t.FullName))}");
        }
    }

    [Fact]
    public void Solution_ShouldNotContain_MediatRPipelineBehaviors()
    {
        foreach (var assembly in AllProjectAssemblies)
        {
            var pipelineTypes = Types.InAssembly(assembly)
                .That()
                .ImplementInterface(typeof(MediatR.IPipelineBehavior<,>))
                .GetTypes();

            Assert.True(!pipelineTypes.Any(),
                $"Assembly {assembly.GetName().Name} must not contain MediatR IPipelineBehavior. " +
                $"Found: {string.Join(", ", pipelineTypes.Select(t => t.FullName))}");
        }
    }

    // ── MediatR usage confined to Domain events only ──

    [Fact]
    public void Application_ShouldNotReference_MediatR_Directly()
    {
        // Application layer should not use MediatR directly.
        // Domain event interfaces (INotification) live in Domain.
        // Dispatching lives in Infrastructure.
        var result = Types.InAssembly(SolutionAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny("MediatR")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application must not reference MediatR directly. " +
            "Domain-event plumbing belongs in Domain (definitions) and Infrastructure (dispatch). " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void ApiService_ShouldNotReference_MediatR()
    {
        var result = Types.InAssembly(SolutionAssemblies.ApiService)
            .ShouldNot()
            .HaveDependencyOnAny("MediatR")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "ApiService must not reference MediatR. Controllers should be thin and delegate to services. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Worker_ShouldNotReference_MediatR()
    {
        var result = Types.InAssembly(SolutionAssemblies.Worker)
            .ShouldNot()
            .HaveDependencyOnAny("MediatR")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Worker must not reference MediatR. Workers should be thin and delegate to services. " +
            $"Violating types: {FormatViolators(result)}");
    }

    private static string FormatViolators(TestResult result)
    {
        var names = result.FailingTypes?.Select(t => t.FullName) ?? [];
        return string.Join(", ", names);
    }
}
