using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>اعتماد وتعديل وحذف المجموعات صراحة مع سجل تدقيق ومنع الانتماء المتعدد.</summary>
public sealed class CategoryGroupManagementService(HiringDb db)
{
    /// <summary>يحفظ قرار المسؤول وحده؛ فهارس SQL وrowversion تحميان من السباقات والتعديل القديم.</summary>
    public async Task<CategoryGroupSaveOutcome> SaveAsync(CategoryGroupSaveDto input, CancellationToken ct)
    {
        var name = CategoryRules.Clean(input.Name);
        var labels = input.Members.Select(CategoryRules.Clean).Where(x => x.Length > 0).DistinctBy(CategoryGroupMap.Key).ToArray();
        if (input.Id < 0 || name.Length is < 1 or > 160 || !CategoryRules.ValidNew(name) || labels.Length == 0
            || labels.Length > 2000 || input.Id > 0 && input.Version.Length != 8)
            return CategoryGroupSaveOutcome.Invalid;
        var reader = new CategoryGroupReadService(db);
        var catalog = (await reader.CatalogAsync(ct)).Select(CategoryGroupMap.Key).ToHashSet(StringComparer.Ordinal);
        if (labels.Any(x => !catalog.Contains(CategoryGroupMap.Key(x)))) return CategoryGroupSaveOutcome.Invalid;
        var keys = labels.Select(CategoryGroupMap.Key).ToArray();
        if (await db.CategoryGroupMembers.AnyAsync(x => keys.Contains(x.NormalizedKey) && x.CategoryGroupId != input.Id, ct))
            return CategoryGroupSaveOutcome.Conflict;
        var group = input.Id == 0 ? new CategoryGroup() : await db.CategoryGroups.Include(x => x.Members).SingleOrDefaultAsync(x => x.Id == input.Id, ct);
        if (group is null) return CategoryGroupSaveOutcome.NotFound;
        if (input.Id > 0) db.Entry(group).Property(x => x.Version).OriginalValue = input.Version;
        else db.CategoryGroups.Add(group);
        group.Name = name;
        group.NormalizedName = CategoryGroupMap.Key(name);
        group.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var removed in group.Members.Where(x => !keys.Contains(x.NormalizedKey)).ToArray()) db.CategoryGroupMembers.Remove(removed);
        foreach (var label in labels)
            if (!group.Members.Any(x => x.NormalizedKey == CategoryGroupMap.Key(label)))
                group.Members.Add(new CategoryGroupMember { Label = label, NormalizedKey = CategoryGroupMap.Key(label) });
        db.Audit.Add(new AuditEntry { Actor = input.Actor.Truncate(200), Action = input.Id == 0 ? "CategoryGroup.Approved" : "CategoryGroup.Updated",
            Detail = $"{name}; {labels.Length} categories".Truncate(2000) });
        return await CommitAsync(ct);
    }
    /// <summary>يحذف المجموعة وانتماءاتها فقط؛ تبقى أسماء الفرص والتفضيلات محفوظة ومستقلة.</summary>
    public async Task<CategoryGroupSaveOutcome> DeleteAsync(int id, byte[] version, string actor, CancellationToken ct)
    {
        if (id <= 0 || version.Length != 8) return CategoryGroupSaveOutcome.Invalid;
        var group = await db.CategoryGroups.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (group is null) return CategoryGroupSaveOutcome.NotFound;
        db.Entry(group).Property(x => x.Version).OriginalValue = version;
        db.CategoryGroups.Remove(group);
        db.Audit.Add(new AuditEntry { Actor = actor.Truncate(200), Action = "CategoryGroup.Deleted", Detail = $"Group {id}; {group.Name}".Truncate(2000) });
        return await CommitAsync(ct);
    }
    /// <summary>يحفظ القرار والتدقيق ذريًا؛ يحول التعارض المتوقع إلى نتيجة قابلة للعرض.</summary>
    private async Task<CategoryGroupSaveOutcome> CommitAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return CategoryGroupSaveOutcome.Saved; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return CategoryGroupSaveOutcome.Conflict; }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { db.ChangeTracker.Clear(); return CategoryGroupSaveOutcome.Conflict; }
    }
}
