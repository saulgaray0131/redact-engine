using NetArchTest.Rules;

namespace RedactEngine.Architecture.Tests;

/// <summary>
/// Enforces data-access conventions from copilot-instructions.md:
///
/// - Use ApplicationDbContext / IApplicationDbContext directly via DI.
/// - Do NOT add repository abstractions for normal CRUD or query composition.
/// - IUnitOfWork is the only persistence-boundary abstraction allowed.
/// - Keep entity configuration, migrations, and DB concerns in Infrastructure.
/// - Domain must not reference EF Core.
/// </summary>
public class DataAccessConventionTests
{
    [Fact]
    public void Domain_ShouldNotReference_EntityFrameworkCore()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain layer must not depend on Entity Framework Core. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Domain_ShouldNotContain_RepositoryInterfaces_BeyondUnitOfWork()
    {
        // IUnitOfWork is allowed; generic repository patterns are not.
        var repoTypes = Types.InAssembly(SolutionAssemblies.Domain)
            .That()
            .ResideInNamespace("RedactEngine.Domain")
            .And()
            .HaveNameMatching("IRepository|IGenericRepository|ICrudRepository|IReadRepository|IWriteRepository")
            .GetTypes();

        Assert.Empty(repoTypes);
    }

    [Fact]
    public void Application_ShouldNotContain_RepositoryInterfaces()
    {
        var repoTypes = Types.InAssembly(SolutionAssemblies.Application)
            .That()
            .HaveNameMatching("IRepository|IGenericRepository|ICrudRepository|IReadRepository|IWriteRepository")
            .GetTypes();

        Assert.Empty(repoTypes);
    }

    [Fact]
    public void Infrastructure_ShouldNotContain_RepositoryAbstractions()
    {
        var repoInterfaces = Types.InAssembly(SolutionAssemblies.Infrastructure)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameMatching("IRepository|IGenericRepository|ICrudRepository")
            .GetTypes();

        Assert.Empty(repoInterfaces);
    }

    [Fact]
    public void Domain_ShouldNotReference_DatabaseProviders()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Npgsql",
                "Microsoft.Data.SqlClient",
                "MySql.Data",
                "MongoDB.Driver")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must not depend on database provider packages. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Application_ShouldNotReference_DatabaseProviders()
    {
        var result = Types.InAssembly(SolutionAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Npgsql",
                "Microsoft.Data.SqlClient",
                "MySql.Data",
                "MongoDB.Driver")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application must not depend on database provider packages. " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Application_ShouldNotReference_AzureStorage()
    {
        var result = Types.InAssembly(SolutionAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny("Azure.Storage.Blobs")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application must not depend directly on Azure Storage. Use abstractions (IBlobService). " +
            $"Violating types: {FormatViolators(result)}");
    }

    [Fact]
    public void Domain_ShouldNotReference_AzureStorage()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Azure.Storage.Blobs")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must not depend on Azure Storage. " +
            $"Violating types: {FormatViolators(result)}");
    }

    private static string FormatViolators(TestResult result)
    {
        var names = result.FailingTypes?.Select(t => t.FullName) ?? [];
        return string.Join(", ", names);
    }
}
