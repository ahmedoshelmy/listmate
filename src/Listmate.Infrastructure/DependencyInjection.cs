using Listmate.Application.Abstractions;
using Listmate.Infrastructure.Persistence;
using Listmate.Infrastructure.Retention;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Listmate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddListmateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not set.");
        services.AddDbContext<ListmateDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IListStore, EfListStore>();
        services.AddOptions<RetentionOptions>()
            .Bind(configuration.GetSection(RetentionOptions.Section))
            .Validate(o => o.ProcessedMutationDays >= 1 && o.SweepEveryHours >= 1, "Retention: days and hours must be at least 1.")
            .ValidateOnStart();
        services.AddScoped<ProcessedMutationPruner>();
        services.AddHostedService<ProcessedMutationRetentionService>();
        services
            .AddIdentityApiEndpoints<IdentityUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ListmateDbContext>();
        return services;
    }
}
