using Listmate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Listmate.Infrastructure.Retention;

/// <summary>
/// <c>Retention:*</c>. A mutation id older than <see cref="ProcessedMutationDays"/> is forgotten, so this must be longer
/// than any device stays offline: a replay after that would be applied again.
/// </summary>
public sealed class RetentionOptions
{
    public const string Section = "Retention";

    public int ProcessedMutationDays { get; set; } = 30;

    public int SweepEveryHours { get; set; } = 6;
}

/// <summary>Deletes processed-mutation rows past the retention window, in one set-based statement.</summary>
public sealed class ProcessedMutationPruner(ListmateDbContext db, TimeProvider clock, IOptions<RetentionOptions> options)
{
    public Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.ProcessedMutationDays);
        return db.ProcessedMutations.Where(p => p.ProcessedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary>
/// Runs the pruner on a timer. Every replica runs it; the delete is idempotent, so the worst case is a duplicate
/// no-op statement.
/// </summary>
internal sealed class ProcessedMutationRetentionService(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<RetentionOptions> options,
    ILogger<ProcessedMutationRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.SweepEveryHours), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var pruned = await scope.ServiceProvider.GetRequiredService<ProcessedMutationPruner>().PruneAsync(stoppingToken);
                if (pruned > 0)
                {
                    logger.LogInformation("Pruned {Count} processed mutations older than {Days} days", pruned, options.Value.ProcessedMutationDays);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Processed-mutation retention sweep failed; trying again next time");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
