using System.Reflection;
using RedactEngine.Domain.Common;

namespace RedactEngine.Architecture.Tests;

/// <summary>
/// Provides assembly references for all projects in the solution,
/// used by architecture tests to inspect types and dependencies.
/// </summary>
public static class SolutionAssemblies
{
    public static readonly Assembly Domain = typeof(Entity).Assembly;

    public static readonly Assembly Application =
        Assembly.Load("RedactEngine.Application");

    public static readonly Assembly Infrastructure =
        Assembly.Load("RedactEngine.Infrastructure");

    public static readonly Assembly ApiService =
        Assembly.Load("RedactEngine.ApiService");

    public static readonly Assembly Worker =
        Assembly.Load("RedactEngine.Worker");

    public static readonly Assembly Shared =
        Assembly.Load("RedactEngine.Shared");

    public static readonly Assembly ServiceDefaults =
        Assembly.Load("RedactEngine.ServiceDefaults");
}
