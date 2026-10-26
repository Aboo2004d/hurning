using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>يسجل فشل نطاق خلفي بعد التخلص منه، دون حفظ تغييرات جزئية أو إخفاء سجل العامل الأصلي.</summary>
public static class OperationFailureRecorder
{
    /// <summary>يحاول حفظ رمز آمن للفشل في نطاق جديد؛ يرجع false إذا تعذر السجل نفسه ولا يعيد تنفيذ العمل.</summary>
    public static async Task<bool> TryRecordAsync(IServiceScopeFactory scopes, OperationArea area,
        string operation, Exception error, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
            db.Audit.Add(OperationTracking.Create(area, OperationOutcome.Failed, operation, ToolFailureDetail.Describe(error, scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>())));
            await db.SaveChangesAsync(timeout.Token);
            return true;
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return false; }
    }
}
