using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>بيانات خرائط الموقع دون XML أو سياق HTTP.</summary>
public sealed class PublicSitemapReadService(HiringDb db)
{
    /// <summary>يحسب الفرص الحالية التي تفاصيلها عامة فقط.</summary>
    public Task<int> CountAsync(DateOnly today, CancellationToken ct) =>
        PublicOpportunityScope.PublicDetails(db.Opportunities.AsNoTracking(), db, today).CountAsync(ct);

    /// <summary>يعيد الأنواع الحالية وتاريخ تغير المحتوى؛ تستخدمه خريطة صفحات القوائم العامة.</summary>
    public Task<List<OpportunityKindChangeDto>> ActiveKindsAsync(DateOnly today, CancellationToken ct) =>
        PublicOpportunityScope.Current(db.Opportunities.AsNoTracking(), today).GroupBy(x => x.Kind)
            .Select(g => new OpportunityKindChangeDto(g.Key, g.Max(x => x.SourceModifiedAt ?? x.SourcePublishedAt ?? x.CreatedAt)))
            .ToListAsync(ct);

    /// <summary>يسقط صفحة ثابتة الترتيب من الفرص المسموح بفهرسة تفاصيلها.</summary>
    public Task<List<SitemapOpportunityDto>> PageAsync(DateOnly today, int page, int pageSize, CancellationToken ct) =>
        PublicOpportunityScope.PublicDetails(db.Opportunities.AsNoTracking(), db, today).OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new SitemapOpportunityDto(x.Id, x.Title, x.SourceModifiedAt ?? x.SourcePublishedAt ?? x.CreatedAt))
            .ToListAsync(ct);
}
