using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>ينفذ دمج الفرص والتراجع عنه مع حماية التزامن وحفظ سجل الاسترجاع.</summary>
public static class MergeHistory
{
    /// <summary>يدمج الفرصة المكررة بعد قفل السجلات ويحفظ أثرًا يمكن التراجع عنه.</summary>
    public static async Task<bool> MergeAsync(
        HiringDb db,
        long duplicateId,
        long targetId,
        string actor,
        CancellationToken ct
    )
    {
        if (duplicateId == targetId)
            return false;
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var duplicate = await db.Opportunities.FindAsync([duplicateId], ct);
        var target = await db.Opportunities.FindAsync([targetId], ct);
        if (duplicate is null || target is null || duplicate.Kind != target.Kind)
            return false;
        if (
            await db.MergeOperations.AnyAsync(
                x => x.UndoneAt == null && x.DuplicateId == duplicateId,
                ct
            )
        )
            return false;
        var links = await db.SourceLinks.Where(x => x.OpportunityId == duplicateId).ToListAsync(ct);
        var record = new MergeOperation
        {
            DuplicateId = duplicateId,
            TargetId = targetId,
            OriginalStatus = duplicate.Status,
            DuplicateOverride = duplicate.AdminOverride,
            TargetOverride = target.AdminOverride,
            OriginalReviewReason = duplicate.ReviewReason,
            Actor = actor,
            LinkIdsJson = JsonSerializer.Serialize(links.Select(x => x.Id).ToArray()),
            TargetBeforeVersion = target.Version.ToArray(),
            DuplicateBeforeVersion = duplicate.Version.ToArray(),
        };
        foreach (var link in links)
            link.OpportunityId = targetId;
        duplicate.Status = PublicationStatus.Hidden;
        duplicate.AdminOverride = target.AdminOverride = true;
        duplicate.ReviewReason = $"Merged by administrator into {targetId}.";
        // Even a second merge into an already protected target must advance its rowversion.
        db.Entry(target).Property(x => x.AdminOverride).IsModified = true;
        await db.SaveChangesAsync(ct);
        record.DuplicateVersion = duplicate.Version.ToArray();
        record.TargetVersion = target.Version.ToArray();
        db.MergeOperations.Add(record);
        db.Audit.Add(
            new()
            {
                Actor = actor,
                Action = "Opportunity.Merge",
                Detail = $"{duplicateId} -> {targetId}",
            }
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    /// <summary>يتراجع عن الدمج فقط عندما تسمح نسخ التزامن باستعادة الحالة بأمان.</summary>
    public static async Task<bool> UndoAsync(
        HiringDb db,
        long operationId,
        string actor,
        CancellationToken ct
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var record = await db.MergeOperations.SingleOrDefaultAsync(x => x.Id == operationId, ct);
        if (record is null)
            return false;
        if (record.UndoneAt is not null)
            return true;
        var duplicate = await db.Opportunities.FindAsync([record.DuplicateId], ct);
        var target = await db.Opportunities.FindAsync([record.TargetId], ct);
        if (
            duplicate is null
            || target is null
            || !duplicate.Version.SequenceEqual(record.DuplicateVersion)
            || !target.Version.SequenceEqual(record.TargetVersion)
        )
            return false;
        var ids = JsonSerializer.Deserialize<long[]>(record.LinkIdsJson) ?? [];
        var links = await db.SourceLinks.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (links.Count != ids.Length || links.Any(x => x.OpportunityId != target.Id))
            return false;
        foreach (var link in links)
            link.OpportunityId = duplicate.Id;
        duplicate.Status = record.OriginalStatus;
        duplicate.AdminOverride = record.DuplicateOverride;
        duplicate.ReviewReason = record.OriginalReviewReason;
        target.AdminOverride = record.TargetOverride;
        db.Entry(target).Property(x => x.AdminOverride).IsModified = true;
        record.UndoneAt = DateTimeOffset.UtcNow;
        db.Audit.Add(
            new()
            {
                Actor = actor,
                Action = "Opportunity.Unmerge",
                Detail = $"Merge {record.Id}: restored {record.DuplicateId} from {record.TargetId}",
            }
        );
        await db.SaveChangesAsync(ct);
        // Returning to the state following an earlier merge lets nested merges be undone in reverse order.
        var earlier = await db
            .MergeOperations.Where(x =>
                x.UndoneAt == null
                && x.Id < record.Id
                && (
                    x.TargetId == target.Id
                    || x.TargetId == duplicate.Id
                    || x.DuplicateId == target.Id
                    || x.DuplicateId == duplicate.Id
                )
            )
            .ToListAsync(ct);
        byte[] RestoredVersion(long id, byte[] priorVersion)
        {
            if (id == target.Id && priorVersion.SequenceEqual(record.TargetBeforeVersion))
                return target.Version.ToArray();
            if (id == duplicate.Id && priorVersion.SequenceEqual(record.DuplicateBeforeVersion))
                return duplicate.Version.ToArray();
            return priorVersion;
        }
        foreach (var prior in earlier)
        {
            prior.TargetVersion = RestoredVersion(prior.TargetId, prior.TargetVersion);
            prior.DuplicateVersion = RestoredVersion(prior.DuplicateId, prior.DuplicateVersion);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
