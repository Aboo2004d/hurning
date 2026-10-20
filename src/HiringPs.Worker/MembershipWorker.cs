using HiringPs.Core;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Worker;

// A separate hosted service keeps bot delivery independent of collection rounds.
public sealed class MembershipWorker(IServiceScopeFactory scopes, ILogger<MembershipWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var tick = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                tick.CancelAfter(TimeSpan.FromMinutes(2));
                await TickAsync(tick.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Do not log request URIs: the Telegram token is part of its endpoint.
                logger.LogWarning(
                    "Membership background work failed ({ExceptionType}). Will retry.",
                    ex.GetType().Name
                );
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }

    /// <summary>يفحص طلبًا داخل نطاق مستقل؛ تعثر طلب لا يمنع بقية الأعضاء ولا يكرر الخصم.</summary>
    private async Task RecoverPaymentAsync(Guid id, string actor, CancellationToken ct)
    {
        await using var paymentScope = scopes.CreateAsyncScope();
        try
        {
            await paymentScope.ServiceProvider.GetRequiredService<PaymentReconciliation>()
                .CheckAsync(id, null, actor, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Payment recovery {OrderId} failed ({ExceptionType}); verification will retry.",
                id, ex.GetType().Name);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramClient>();
        var zeroOrders = await db
            .PaymentOrders.AsNoTracking()
            .Where(x =>
                x.GatewayEnvironment == db.ActivePaymentEnvironment
                && x.Gateway == "discount"
                && x.AmountIls == 0
                && x.DiscountPercent == 100
                && x.State == PaymentState.AwaitingPayment
            )
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(5)
            .ToListAsync(ct);
        foreach (var id in zeroOrders)
            await RecoverPaymentAsync(id, "Discount recovery", ct);
        var now = DateTimeOffset.UtcNow;
        var recovery = scope.ServiceProvider.GetRequiredService<PaymentRecoveryService>();
        var pending = await recovery.CandidatesAsync(ct);
        foreach (var id in pending)
            await RecoverPaymentAsync(id, "Payment reconciliation", ct);
        if (!telegram.Ready)
            return;
        if (
            await db
                .MembershipRuntimeStates.Where(x =>
                    x.Id == 1 && (x.LockedUntil == null || x.LockedUntil < now)
                )
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.LockedUntil, now.AddMinutes(5)), ct)
            == 0
        )
            return;
        try
        {
            var command = scope.ServiceProvider.GetRequiredService<TelegramCommands>();
            var inbox = await db
                .TelegramInbox.Where(x =>
                    x.ProcessedAt == null
                    && x.Attempts < 8
                    && (x.LockedUntil == null || x.LockedUntil < now)
                )
                .OrderBy(x => x.Id)
                .Take(3)
                .ToListAsync(ct);
            foreach (var update in inbox)
            {
                update.Attempts++;
                update.LockedUntil = now.AddMinutes(1);
                await db.SaveChangesAsync(ct);
                try
                {
                    await command.HandleAsync(update.Json, ct);
                    update.ProcessedAt = DateTimeOffset.UtcNow;
                    update.Json = "{}"; // Contact payloads are discarded after processing.
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        "Telegram update {UpdateId} could not be processed ({ExceptionType}).",
                        update.Id,
                        ex.GetType().Name
                    );
                }
                await db.SaveChangesAsync(ct);
            }
            var notifications = scope.ServiceProvider.GetRequiredService<MemberNotifications>();
            await notifications.ExpandPublicationsAsync(ct);
            await notifications.RemindersAsync(ct);
            await notifications.DeliverAsync(ct);
            // Retain update IDs briefly for duplicate detection without retaining contact data.
            await db
                .TelegramInbox.Where(x => x.ReceivedAt < now.AddDays(-7))
                .ExecuteDeleteAsync(ct);
        }
        finally
        {
            await db
                .MembershipRuntimeStates.Where(x => x.Id == 1)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(y => y.LockedUntil, (DateTimeOffset?)null),
                    CancellationToken.None
                );
        }
    }
}
