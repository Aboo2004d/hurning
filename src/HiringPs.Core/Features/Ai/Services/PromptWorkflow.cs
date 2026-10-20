using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>مكون PromptWorkflow ضمن مجال Ai؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class PromptWorkflow
{
    // Only the worker activates pending versions, while holding the collection SQL lock.
    /// <summary>Only the worker activates pending versions, while holding the collection SQL lock.</summary>
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
        // The full requeue scan runs only after an activation (plus an hourly sweep for
        // re-enabled sources), not on every few-second tick.
        var state = await AiGovernor.StateAsync(db, ct);
        var now = DateTimeOffset.UtcNow;
        if (
            state.LastRequeuedPromptVersionId == active.Id
            && state.LastRequeuedAt > now.AddHours(-1)
        )
            return active;
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
        state.LastRequeuedPromptVersionId = active.Id;
        state.LastRequeuedAt = now;
        await db.SaveChangesAsync(ct);
        return active;
    }

    /// <summary>ينفذ Protected وفق قواعد PromptWorkflow الحالية دون تغيير عقد المستدعين.</summary>
    public static bool Protected(Opportunity opportunity, int? incomingVersion) =>
        opportunity.AdminOverride
        || (
            incomingVersion is not null
            && incomingVersion != opportunity.PromptVersionId
            && opportunity.Status is PublicationStatus.Published or PublicationStatus.Archived
        );
}
