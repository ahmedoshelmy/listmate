using System.Reflection;
using Listmate.Application.Sync;
using Listmate.Domain.Lists;
using Listmate.Infrastructure.Persistence;

namespace Listmate.UnitTests;

/// <summary>The Clean Architecture dependency rule, checked on the compiled assemblies.</summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(SharedList).Assembly;
    private static readonly Assembly Application = typeof(SyncService).Assembly;
    private static readonly Assembly Infrastructure = typeof(ListmateDbContext).Assembly;

    private static string[] References(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(a => a.Name!)];

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_library()
    {
        References(Domain).ShouldAllBe(name => name.StartsWith("System", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_depends_on_domain_but_not_on_infrastructure_or_web()
    {
        var references = References(Application);

        references.ShouldContain(Domain.GetName().Name!);
        references.ShouldNotContain(Infrastructure.GetName().Name!);
        references.ShouldNotContain(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        references.ShouldNotContain(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api()
    {
        References(Infrastructure).ShouldNotContain("Listmate.Api");
    }
}
