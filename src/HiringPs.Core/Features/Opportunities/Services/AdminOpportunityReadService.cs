using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قوائم إدارة الفرص وعداداتها عبر إسقاطات للعرض.</summary>
public sealed class AdminOpportunityReadService(HiringDb db)
{
    /// <summary>يرتب الفرص بالحالة المطلوبة ويحسب أهلية النشر بنفس سياسة الإغلاق والموعد.</summary>
    public IQueryable<AdminOpportunitySummaryDto> Opportunities(PublicationStatus? status)
    {
        var today = TextRules.PalestineToday();
        return db.Opportunities.AsNoTracking().Where(x => status == null || x.Status == status)
            .OrderByDescending(x => x.Id).Select(x => new AdminOpportunitySummaryDto(
                x.Id, x.Title, x.Kind, x.Organization, x.Status, x.ReviewReason,
                x.Status == PublicationStatus.Published && (x.Deadline == null || x.Deadline >= today)
                    && string.IsNullOrWhiteSpace(x.ClosureEvidence)));
    }

    /// <summary>يحسب عدادات الحالات لعلامات تبويب الإدارة.</summary>
    public Task<Dictionary<PublicationStatus, int>> CountsAsync(CancellationToken ct) =>
        db.Opportunities.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
}
