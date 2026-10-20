using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Lists merge history and delegates guarded restoration to the existing merge transaction.</summary>
public sealed class MergeAdministrationService(HiringDb db)
{
    /// <summary>Filters either side of a merge and projects its summary before pagination.</summary>
    public IQueryable<MergeOperationSummaryDto> History(long? opportunityId)
    {
        var query = db.MergeOperations.AsNoTracking();
        if (opportunityId is { } id)
            query = query.Where(x => x.DuplicateId == id || x.TargetId == id);
        return query.OrderByDescending(x => x.Id).Select(x => new MergeOperationSummaryDto(x.Id, x.DuplicateId, x.TargetId, x.CreatedAt, x.UndoneAt));
    }

    /// <summary>Restores only when MergeHistory's snapshot and concurrency checks permit it.</summary>
    public async Task<bool> UndoAsync(long id, string actor, CancellationToken ct)
    {
        try
        {
            return await MergeHistory.UndoAsync(db, id, actor, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
