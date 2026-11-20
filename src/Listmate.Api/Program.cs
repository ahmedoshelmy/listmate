using Listmate.Api.Endpoints;
using Listmate.Api.Hosting;
using Listmate.Api.Hubs;
using Listmate.Application;
using Listmate.Application.Abstractions;
using Listmate.Infrastructure;
using Listmate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;

await HealthProbe.RunIfRequestedAsync(args);

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability();
builder.Services
    .AddListmateApplication()
    .AddListmateInfrastructure(builder.Configuration);

// Browsers cannot set headers on a WebSocket: the hub reads the bearer token from ?access_token= instead.
builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, o => o.Events = new BearerTokenEvents
{
    OnMessageReceived = context =>
    {
        var token = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    },
});
builder.Services.AddAuthorization();
builder.AddScaleOut(builder.Services.AddSignalR());
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddScoped<IListNotifier, SignalRListNotifier>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks().AddReadyCheck<ListmateDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

await app.PrepareDatabaseAsync();

app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>().WithTags("Auth");
app.MapListEndpoints();
app.MapHub<ListsHub>("/hubs/lists");
app.MapHealthEndpoints();

await app.RunAsync();
