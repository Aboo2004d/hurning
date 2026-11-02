using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>Interactive updates have their own short polling loop and distributed lease, independent of payment/delivery work.</summary>
public sealed class TelegramInboxWorker(IServiceScopeFactory scopes, ILogger<TelegramInboxWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(ProcessLoopAsync(stoppingToken), SynchronizeLoopAsync(stoppingToken));

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var tick = CancellationTokenSource.CreateLinkedTokenSource(ct);
                tick.CancelAfter(TimeSpan.FromSeconds(90));
                await TickAsync(tick.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Telegram inbox processing failed ({ExceptionType}); will retry.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromMilliseconds(500), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task SynchronizeLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var success = false;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var telegram = scope.ServiceProvider.GetRequiredService<TelegramClient>();
                if ((ApplicationRuntime.Mode(config) != ApplicationRuntimeMode.Local || BackgroundRegistration.LocalTelegramTesting(config)) && telegram.Ready)
                    await telegram.SynchronizeCommandsAsync(ct);
                success = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Telegram command menu refresh failed ({ExceptionType}); will retry.", ex.GetType().Name); }
            try { await Task.Delay(success ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(1), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (ApplicationRuntime.Mode(config) == ApplicationRuntimeMode.Local && !BackgroundRegistration.LocalTelegramTesting(config)) return;
        if (!scope.ServiceProvider.GetRequiredService<TelegramClient>().Ready) return;
        var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
        // Row 1 belongs to notifications. Row 2 is an independent inbox lease; no schema change is needed.
        if (!await db.MembershipRuntimeStates.AnyAsync(x => x.Id == 2, ct))
        {
            var state = new MembershipRuntimeState { Id = 2 };
            db.MembershipRuntimeStates.Add(state);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(state).State = EntityState.Detached;
                if (!await db.MembershipRuntimeStates.AnyAsync(x => x.Id == 2, ct)) throw;
            }
        }
        var now = DateTimeOffset.UtcNow;
        var lease = now.AddMinutes(2);
        if (await db.MembershipRuntimeStates.Where(x => x.Id == 2 && (x.LockedUntil == null || x.LockedUntil < now))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.LockedUntil, lease), ct) == 0) return;
        try
        {
            var updates = await db.TelegramInbox.Where(x => x.ProcessedAt == null && x.Attempts < 8
                && (x.LockedUntil == null || x.LockedUntil < now)).OrderBy(x => x.Id).Take(20).ToListAsync(ct);
            foreach (var update in updates)
            {
                ct.ThrowIfCancellationRequested();
                update.Attempts++;
                update.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
                await db.SaveChangesAsync(ct);
                // Each command gets a fresh context and response scope, including after a failed command.
                await using var commandScope = scopes.CreateAsyncScope();
                try
                {
                    await commandScope.ServiceProvider.GetRequiredService<TelegramCommands>().HandleAsync(update.Json, ct);
                    update.ProcessedAt = DateTimeOffset.UtcNow;
                    update.Json = "{}";
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    db.Audit.Add(OperationTracking.Create(OperationArea.Telegram, OperationOutcome.Failed,
                        "Update", $"Update {update.Id}; Attempt {update.Attempts}; {ex.GetType().Name}"));
                    logger.LogWarning("Telegram update {UpdateId} failed ({ExceptionType}).", update.Id, ex.GetType().Name);
                }
                await db.SaveChangesAsync(ct);
            }
        }
        finally
        {
            await db.MembershipRuntimeStates.Where(x => x.Id == 2 && x.LockedUntil == lease)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.LockedUntil, (DateTimeOffset?)null), CancellationToken.None);
        }
    }
}
