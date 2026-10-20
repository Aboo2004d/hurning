using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads prompt snapshots and queues immutable new versions for activation by the worker.</summary>
public sealed class PromptAdministrationService(HiringDb db)
{
    /// <summary>Reads global activation pointers and their rowversion without tracking the entity.</summary>
    public Task<PromptSettingsDto> SettingsAsync(CancellationToken ct) => db.WorkflowSettings.AsNoTracking()
        .Where(x => x.Id == 1).Select(x => new PromptSettingsDto(x.ActivePromptVersionId, x.PendingPromptVersionId, x.Version)).SingleAsync(ct);

    /// <summary>Reads the input fields of an existing version for inspection or copying.</summary>
    public Task<PromptEditDto?> ReadAsync(int id, CancellationToken ct) => db.PromptVersions.AsNoTracking()
        .Where(x => x.Id == id).Select(x => new PromptEditDto(x.Id, x.Template, x.SummaryLimit, x.GeographicScope, x.AllowedKinds, x.AllowUnknownDeadline))
        .SingleOrDefaultAsync(ct);

    /// <summary>Projects newest history rows before pagination, including their stored final text.</summary>
    public IQueryable<PromptVersionSummaryDto> History() => db.PromptVersions.AsNoTracking().OrderByDescending(x => x.Id)
        .Select(x => new PromptVersionSummaryDto(x.Id, x.CreatedAt, x.Note, x.FinalText, x.LegacySnapshot));

    /// <summary>Creates the version and pending pointer in one transaction; stale workflow tokens roll the entire change back.</summary>
    public async Task<RecordUpdateOutcome> SaveAsync(PromptVersionWriteDto input, string actor, CancellationToken ct)
    {
        var settings = await db.WorkflowSettings.SingleAsync(x => x.Id == 1, ct);
        db.Entry(settings).Property(x => x.Version).OriginalValue = input.Version;
        var prompt = new PromptVersion
        {
            Template = input.Template,
            SummaryLimit = input.SummaryLimit,
            GeographicScope = input.GeographicScope.Trim(),
            AllowedKinds = input.AllowedKinds,
            AllowUnknownDeadline = input.AllowUnknownDeadline,
            Note = input.Note,
            CreatedBy = actor,
        };
        prompt.FinalText = prompt.Compose();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PromptVersions.Add(prompt);
            await db.SaveChangesAsync(ct);
            settings.PendingPromptVersionId = prompt.Id;
            db.Audit.Add(new()
            {
                Actor = actor,
                Action = "Prompt.VersionCreated",
                Detail = $"Version {prompt.Id}; activation queued for the next worker round.",
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return RecordUpdateOutcome.Conflict;
        }
        return RecordUpdateOutcome.Saved;
    }

    /// <summary>Builds an unsaved preview through the same composition rules used for persisted prompts.</summary>
    public static string Preview(string? template, string? scope, string? kinds, int limit, bool allowUnknownDeadline) =>
        new PromptVersion
        {
            Template = template ?? "",
            GeographicScope = scope ?? "",
            AllowedKinds = kinds ?? "",
            SummaryLimit = limit,
            AllowUnknownDeadline = allowUnknownDeadline,
        }.Render();
}
