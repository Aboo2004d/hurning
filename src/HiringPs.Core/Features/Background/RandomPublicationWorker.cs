using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace HiringPs.Core;

/// <summary>عامل سيرفر مستقل للنشر العشوائي؛ لا يشغل متصفحًا أو جمع مصادر.</summary>
public sealed class RandomPublicationWorker(IServiceScopeFactory scopes, ILogger<RandomPublicationWorker> logger) : BackgroundService
{
    /// <summary>يفحص المواعيد كل نصف دقيقة مع عزل الفشل عن الدفع والتليجرام.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromMinutes(3));
                await scope.ServiceProvider.GetRequiredService<RandomPublicationScheduler>().TickAsync(budget.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                await OperationFailureRecorder.TryRecordAsync(scopes, OperationArea.Publishing, "RandomPublication", ex, stoppingToken);
                logger.LogWarning("Random publication cycle failed ({Type}).", ex.GetType().Name);
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
