using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Listmate.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations add` build the model without a running database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ListmateDbContext>
{
    public ListmateDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ListmateDbContext>().UseNpgsql("Host=localhost;Database=listmate").Options);
}
