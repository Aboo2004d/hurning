using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

public static class AiUsageRecorder
{
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

public static class PromptWorkflow
{
    // Only the worker activates pending versions, while holding the collection SQL lock.
    public static async Task<PromptVersion> ActivateAsync(HiringDb db, CancellationToken ct)
    {
        var settings = await db.WorkflowSettings.SingleAsync(x => x.Id == 1, ct);
        if (settings.PendingPromptVersionId is { } pending)
        {
            settings.ActivePromptVersionId = pending;
            settings.PendingPromptVersionId = null;
            db.Audit.Add(
                new()
                {
                    Actor = "Worker",
                    Action = "Prompt.Activated",
                    Detail = $"Prompt version {pending}",
                }
            );
            await db.SaveChangesAsync(ct);
        }
        var active = await db
            .PromptVersions.AsNoTracking()
            .SingleAsync(x => x.Id == settings.ActivePromptVersionId, ct);
        // Published, archived and manual decisions are not queued for prompt changes.
        var eligible = db.SourceItems.Where(item =>
            item.RequestedPromptVersionId != active.Id
            && item.Source.Enabled
            && item.Source.UsageApproved
            && (
                !db.SourceLinks.Any(l => l.SourceItemId == item.Id)
                || db.SourceLinks.Any(l =>
                    l.SourceItemId == item.Id
                    && !l.Opportunity.AdminOverride
                    && l.Opportunity.Status != PublicationStatus.Published
                    && l.Opportunity.Status != PublicationStatus.Archived
                )
            )
        );
        await eligible.ExecuteUpdateAsync(
            s =>
                s.SetProperty(x => x.RequestedPromptVersionId, active.Id)
                    .SetProperty(
                        x => x.Status,
                        x =>
                            x.Content == ""
                                ? ProcessingStatus.Discovered
                                : ProcessingStatus.AwaitingAi
                    )
                    .SetProperty(x => x.RetryAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.Attempts, 0)
                    .SetProperty(x => x.Error, ""),
            ct
        );
        return active;
    }

    public static bool Protected(Opportunity opportunity, int? incomingVersion) =>
        opportunity.AdminOverride
        || (
            incomingVersion is not null
            && incomingVersion != opportunity.PromptVersionId
            && opportunity.Status is PublicationStatus.Published or PublicationStatus.Archived
        );
}

public static class RetentionCleanup
{
    public static async Task RunAsync(HiringDb db, IConfiguration config, CancellationToken ct)
    {
        var policy = await db.RetentionPolicies.SingleAsync(x => x.Id == 1, ct);
        if (!policy.Enabled || policy.LastCleanupAt > DateTimeOffset.UtcNow.AddHours(-24))
            return;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-policy.RevisionDays);
        policy.LastDeletedRevisions = await db
            .Revisions.Where(x => x.CapturedAt < cutoff)
            .ExecuteDeleteAsync(ct);
        var path = CollectorDiagnosticFiles.DirectoryPath(config);
        var removed = 0;
        if (Directory.Exists(path))
        {
            foreach (
                var file in new DirectoryInfo(path)
                    .EnumerateFiles()
                    .Where(x =>
                        x.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-policy.DiagnosticDays)
                    )
                    .Take(200)
            )
            {
                ct.ThrowIfCancellationRequested();
                if (
                    file.Extension is not (".jpg" or ".json")
                    || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "N", out _)
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0
                )
                    continue;
                try
                {
                    file.Delete();
                    removed++;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        policy.LastDeletedDiagnostics = removed;
        policy.LastCleanupAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
