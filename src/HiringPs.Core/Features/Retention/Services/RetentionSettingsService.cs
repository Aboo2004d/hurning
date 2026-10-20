using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads and updates cleanup policy; never performs cleanup from a web request.</summary>
public sealed class RetentionSettingsService(HiringDb db)
{
    /// <summary>Projects the global cleanup policy and its last-run metrics.</summary>
    public Task<RetentionSettingsDto> ReadAsync(CancellationToken ct) => db.RetentionPolicies.AsNoTracking()
        .Where(x => x.Id == 1).Select(x => new RetentionSettingsDto(x.Enabled, x.RevisionDays, x.DiagnosticDays,
            x.LastCleanupAt, x.LastDeletedRevisions, x.LastDeletedDiagnostics, x.Version)).SingleAsync(ct);

    /// <summary>Saves an administrator edit under rowversion protection and makes the worker's next cleanup eligible.</summary>
    public async Task<RecordUpdateOutcome> SaveAsync(RetentionUpdateDto input, string actor, CancellationToken ct)
    {
        var policy = await db.RetentionPolicies.SingleAsync(x => x.Id == 1, ct);
        db.Entry(policy).Property(x => x.Version).OriginalValue = input.Version;
        policy.Enabled = input.Enabled;
        policy.RevisionDays = input.RevisionDays;
        policy.DiagnosticDays = input.DiagnosticDays;
        policy.LastCleanupAt = null;
        db.Audit.Add(new()
        {
            Actor = actor,
            Action = "Retention.Updated",
            Detail = $"Enabled={policy.Enabled}; revisions={policy.RevisionDays}; diagnostics={policy.DiagnosticDays}",
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RecordUpdateOutcome.Conflict;
        }
        return RecordUpdateOutcome.Saved;
    }
}
