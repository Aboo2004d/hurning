using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءات المجموعات والكتالوج دون تعديل تصنيفات الفرص أو تشغيل مطابقة تلقائية.</summary>
public sealed class CategoryGroupReadService(HiringDb db)
{
    /// <summary>يحمّل خريطة المعتمد فقط لاستخدامها في حد التفضيلات والتوزيع.</summary>
    public async Task<CategoryGroupMap> MapAsync(CancellationToken ct)
    {
        var members = await db.CategoryGroupMembers.AsNoTracking()
            .Select(x => new { x.Label, x.CategoryGroupId, x.CategoryGroup.Name }).ToArrayAsync(ct);
        return new(members.Select(x => (x.Label, x.CategoryGroupId, x.Name)));
    }
    /// <summary>قائمة قابلة للترقيم في SQL؛ البحث يشمل المجموعة وأسماءها الأصلية.</summary>
    public IQueryable<CategoryGroupSummaryDto> Groups(string? search)
    {
        var query = db.CategoryGroups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().Truncate(200);
            query = query.Where(x => x.Name.Contains(value) || x.Members.Any(m => m.Label.Contains(value)));
        }
        return Project(query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id));
    }
    /// <summary>يقرأ محرر مجموعة محددة أو null عند حذفها.</summary>
    public Task<CategoryGroupSummaryDto?> FindAsync(int id, CancellationToken ct) =>
        Project(db.CategoryGroups.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);

    /// <summary>يسقط الحقول بعد ترشيح الكيان؛ الترشيح على record بعد إنشائه غير قابل للترجمة في EF.</summary>
    private static IQueryable<CategoryGroupSummaryDto> Project(IQueryable<CategoryGroup> query) =>
        query.Select(x => new CategoryGroupSummaryDto(x.Id, x.Name, x.UpdatedAt,
            x.Members.OrderBy(m => m.Label).Select(m => m.Label).ToArray(), x.Version));

    /// <summary>يعيد أسماء الفرص والانتماءات التاريخية فقط؛ لا يقرأ اهتمامات الأعضاء الخاصة.</summary>
    public async Task<string[]> CatalogAsync(CancellationToken ct)
    {
        var values = await db.Opportunities.AsNoTracking().Select(x => x.Category)
            .Concat(db.CategoryGroupMembers.AsNoTracking().Select(x => x.Label)).Where(x => x != "").Distinct().ToArrayAsync(ct);
        return values.Concat(CategoryTaxonomy.Labels).Select(CategoryRules.Clean).Where(x => x.Length > 0).OrderBy(x => x, StringComparer.Ordinal)
            .DistinctBy(CategoryGroupMap.Key).ToArray();
    }
    /// <summary>يعيد الانتماءات القائمة لتقييد اختيار المجال بمجموعة واحدة.</summary>
    public Task<Dictionary<string, int>> AssignmentsAsync(CancellationToken ct) => db.CategoryGroupMembers.AsNoTracking()
        .ToDictionaryAsync(x => x.NormalizedKey, x => x.CategoryGroupId, ct);
}
