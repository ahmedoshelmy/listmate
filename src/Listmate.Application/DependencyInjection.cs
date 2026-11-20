using FluentValidation;
using Listmate.Application.Lists;
using Listmate.Application.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace Listmate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddListmateApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<SyncRequestValidator>(includeInternalTypes: true);
        services.AddScoped<ListService>();
        services.AddScoped<SyncService>();
        return services;
    }
}
