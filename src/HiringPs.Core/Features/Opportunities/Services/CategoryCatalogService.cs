using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>كتالوج المجالات الموجودة لاختيار تسمية متسقة أثناء تحرير الفرص.</summary>
public sealed class CategoryCatalogService(HiringDb db) : ICategoryCatalogReadService
{
    /// <summary>يعيد تسميات المجالات فقط دون تحميل محتوى الفرص.</summary>
    public async Task<string[]> OptionsAsync(CancellationToken ct)
    {
        var values = await db.Opportunities.AsNoTracking().Where(x => x.Category != "")
            .Select(x => x.Category).Distinct().ToArrayAsync(ct);
        return values.Concat(CategoryTaxonomy.Labels).Select(CategoryRules.Clean).Where(x => x.Length > 0)
            .DistinctBy(CategoryRules.Key).OrderBy(x => x).ToArray();
    }

}
