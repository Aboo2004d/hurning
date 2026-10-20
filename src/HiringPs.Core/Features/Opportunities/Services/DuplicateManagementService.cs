using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يعرض أزواج التكرار ويفوض الدمج القابل للتراجع ويحفظ قرارات الاستبعاد.</summary>
public sealed class DuplicateManagementService(HiringDb db)
{
    /// <summary>يخفي الأزواج المفتوحة التي أخفي أحد طرفيها ويحافظ على ترتيب الإدارة الحالي.</summary>
    public IQueryable<DuplicateCandidateSummaryDto> Candidates(DuplicateCandidateStatus status)
    {
        var query = db.DuplicateCandidates.AsNoTracking().Where(x => x.Status == status);
        if (status == DuplicateCandidateStatus.Open)
            query = query.Where(x => x.Opportunity.Status != PublicationStatus.Hidden && x.Candidate.Status != PublicationStatus.Hidden);
        return query.OrderByDescending(x => x.Id).Select(x => new DuplicateCandidateSummaryDto(
            x.Id, x.OpportunityId, x.CandidateId,
            new DuplicateOpportunitySummaryDto(x.Opportunity.Id, x.Opportunity.Title, x.Opportunity.Organization, x.Opportunity.Status, x.Opportunity.Deadline),
            new DuplicateOpportunitySummaryDto(x.Candidate.Id, x.Candidate.Title, x.Candidate.Organization, x.Candidate.Status, x.Candidate.Deadline),
            x.Reason, x.Status, x.MergeOperationId));
    }

    /// <summary>يدمج الطرف الجديد بالقديم أو يعكس الاتجاه بطلب الإدارة، مع حماية التزامن الأصلية.</summary>
    public async Task<DuplicateMergeResultDto> MergeAsync(long id, bool reverse, string actor, CancellationToken ct)
    {
        var pair = await db.DuplicateCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (pair is null) return new(DuplicateActionOutcome.NotFound);
        if (pair.Status != DuplicateCandidateStatus.Open) return new(DuplicateActionOutcome.Unchanged);
        var duplicate = reverse ? pair.CandidateId : pair.OpportunityId;
        var target = reverse ? pair.OpportunityId : pair.CandidateId;
        bool merged;
        try { merged = await MergeHistory.MergeAsync(db, duplicate, target, actor, ct); }
        catch (DbUpdateConcurrencyException) { merged = false; }
        return new(merged ? DuplicateActionOutcome.Merged : DuplicateActionOutcome.Failed, duplicate, target);
    }

    /// <summary>يستبعد زوجًا مفتوحًا ويسجل الفاعل دون إخفاء أي فرصة.</summary>
    public async Task<bool> DismissAsync(long id, string actor, CancellationToken ct)
    {
        var pair = await db.DuplicateCandidates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (pair is null) return false;
        if (pair.Status == DuplicateCandidateStatus.Open)
        {
            pair.Status = DuplicateCandidateStatus.Dismissed;
            pair.ResolvedAt = DateTimeOffset.UtcNow;
            pair.ResolvedBy = actor.Truncate(256);
            db.Audit.Add(new AuditEntry { Actor = actor, Action = "Duplicate.Dismiss", Detail = $"{pair.OpportunityId} / {pair.CandidateId}" });
            await db.SaveChangesAsync(ct);
        }
        return true;
    }
}
