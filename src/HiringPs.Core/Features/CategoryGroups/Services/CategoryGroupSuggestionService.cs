using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يحسب الاقتراحات للصفحة الحالية فقط؛ لا يكتب مجموعات أو يوسع التنبيهات.</summary>
public sealed class CategoryGroupSuggestionService(HiringDb db)
{
    /// <summary>يجمع الأسماء غير المصنفة ويقترح الأقرب من المستقل أو المعتمد، دون اعتماد تلقائي.</summary>
    public async Task<UngroupedCategoryPageDto> PageAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var reader = new CategoryGroupReadService(db);
        var catalog = await reader.CatalogAsync(ct);
        var assigned = await reader.AssignmentsAsync(ct);
        var groups = await db.CategoryGroups.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        // A concurrent ungrouping may remove a group between these read-only queries.
        assigned = assigned.Where(entry => groups.ContainsKey(entry.Value)).ToDictionary(entry => entry.Key, entry => entry.Value);
        var pending = catalog.Where(x => !assigned.ContainsKey(CategoryGroupMap.Key(x)))
            .Where(x => string.IsNullOrWhiteSpace(search) || CategoryRules.Key(x).Contains(CategoryRules.Key(search), StringComparison.Ordinal)).ToArray();
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)pending.Length / pageSize)));
        var items = pending.Skip((page - 1) * pageSize).Take(pageSize).Select(label =>
        {
            var suggestions = catalog.Where(other => CategoryGroupMap.Key(other) != CategoryGroupMap.Key(label)
                    && CategorySimilarityRules.Suggests(label, other))
                .Select(other =>
                {
                    var grouped = assigned.TryGetValue(CategoryGroupMap.Key(other), out var id);
                    var known = CategorySimilarityRules.KnownEquivalent(label, other);
                    var percent = known ? 100 : (int)Math.Ceiling(100 * CategorySimilarityRules.Similarity(
                        CategorySimilarityRules.Normalize(label), CategorySimilarityRules.Normalize(other)));
                    return new CategorySuggestionDto(other, grouped ? id : null, grouped ? groups[id] : null, percent, known);
                }).OrderByDescending(x => x.Percent).ThenBy(x => x.Label, StringComparer.Ordinal).Take(5).ToArray();
            return new UngroupedCategoryDto(label, suggestions);
        }).ToArray();
        return new(page, pending.Length, items);
    }
}
