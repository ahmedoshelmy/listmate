using Listmate.Application.Lists;
using Listmate.Application.Sync;
using Listmate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Listmate.Api.Hosting;

internal sealed class ListmateOptions
{
    public const string Section = "Listmate";

    /// <summary>Apply EF migrations when the API starts (compose, tests). In Azure this is a release step instead.</summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Create the two demo accounts and their lists on an empty database.</summary>
    public bool SeedDemoData { get; set; }

    public string? DemoPassword { get; set; }
}

internal static class DatabaseStartup
{
    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        var options = app.Configuration.GetSection(ListmateOptions.Section).Get<ListmateOptions>() ?? new ListmateOptions();
        var ct = app.Lifetime.ApplicationStopping;
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (options.MigrateOnStartup)
        {
            await services.GetRequiredService<ListmateDbContext>().Database.MigrateAsync(ct);
        }

        if (options.SeedDemoData)
        {
            await SeedAsync(services, options.DemoPassword ?? throw new InvalidOperationException("Listmate:DemoPassword is required to seed demo data."), ct);
            app.Logger.LogInformation("Demo data ready");
        }
    }

    /// <summary>Invented accounts and lists so the app has something to show; runs only on an empty database.</summary>
    private static async Task SeedAsync(IServiceProvider services, string password, CancellationToken ct)
    {
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.FindByEmailAsync("alex@example.com") is not null)
        {
            return;
        }

        var alex = await CreateUserAsync(users, "alex@example.com", password);
        var sam = await CreateUserAsync(users, "sam@example.com", password);
        var lists = services.GetRequiredService<ListService>();
        var sync = services.GetRequiredService<SyncService>();

        await SeedListAsync(lists, sync, alex.Id, sam.Id, "Weekly groceries", ["2x milk", "eggs x12", "bread", "tomatoes", "coffee"], ct);
        await SeedListAsync(lists, sync, alex.Id, sam.Id, "Beach trip packing", ["sunscreen", "4 towels", "umbrella", "snacks"], ct);
    }

    private static async Task SeedListAsync(ListService lists, SyncService sync, string ownerId, string memberId, string name, string[] items, CancellationToken ct)
    {
        var list = await lists.CreateAsync(ownerId, new CreateListRequest(name), ct);
        await lists.JoinAsync(memberId, new JoinListRequest(list.InviteCode), ct);
        var mutations = items.Select(text => new MutationDto(Guid.NewGuid(), MutationTypes.Add, list.Id, Guid.NewGuid(), Text: text)).ToList();
        await sync.ApplyAsync(ownerId, new SyncRequest(mutations), ct);
    }

    private static async Task<IdentityUser> CreateUserAsync(UserManager<IdentityUser> users, string email, string password)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, password);
        return result.Succeeded
            ? user
            : throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
