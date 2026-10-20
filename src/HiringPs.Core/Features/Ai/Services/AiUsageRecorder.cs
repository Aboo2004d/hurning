using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>يحفظ بداية استدعاء AI ونتيجته باستخدام نطاق قاعدة مستقل عن عمر الطلب الأصلي.</summary>
public static class AiUsageRecorder
{
    /// <summary>يحفظ بداية استدعاء AI ونسخة التعليمات المستخدمة لتتبع استهلاكه.</summary>
    public static async Task<long> StartAsync(
        IServiceScopeFactory scopes,
        AiExecution execution,
        string model,
        CancellationToken ct
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
        var usage = new AiUsage
        {
            PromptVersionId = execution.Version.Id,
            SourceItemId = execution.SourceItemId,
            Model = model,
            PromptHash = TextRules.Hash(execution.Version.Render()),
        };
        db.AiUsage.Add(usage);
        await db.SaveChangesAsync(ct);
        return usage.Id;
    }

    /// <summary>يحفظ نتيجة استدعاء AI واستهلاك الوحدات وفق البيانات المتاحة.</summary>
    public static async Task FinishAsync(
        IServiceScopeFactory scopes,
        long id,
        AiTelemetry data,
        string? error,
        long duration
    )
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
        await db
            .AiUsage.Where(x => x.Id == id)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow)
                        .SetProperty(x => x.DurationMs, duration)
                        .SetProperty(x => x.HttpStatus, data.HttpStatus)
                        .SetProperty(x => x.Success, error == null)
                        .SetProperty(x => x.ErrorType, error ?? "")
                        .SetProperty(x => x.InputTokens, data.Input)
                        .SetProperty(x => x.OutputTokens, data.Output)
                        .SetProperty(x => x.CachedTokens, data.Cached)
                        .SetProperty(x => x.TotalTokens, data.Total),
                budget.Token
            );
    }
}
