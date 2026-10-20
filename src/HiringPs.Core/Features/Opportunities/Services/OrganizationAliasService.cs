using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يدير الأسماء البديلة دون تغيير اسم الجهة المعروض أو السماح بسلاسل أسماء غامضة.</summary>
public sealed class OrganizationAliasService(HiringDb db)
{
    /// <summary>يعيد قائمة الأسماء مرتبة بالاسم المعتمد ثم البديل قبل ترقيمها.</summary>
    public IQueryable<OrganizationAliasSummaryDto> Aliases() => db.OrganizationAliases.AsNoTracking()
        .OrderBy(x => x.Canonical).ThenBy(x => x.Alias)
        .Select(x => new OrganizationAliasSummaryDto(x.Id, x.Alias, x.Canonical, x.CreatedBy, x.CreatedAt));

    /// <summary>يطبع الاسمين ويربط البديل مباشرة بالاسم النهائي مع إبقاء قواعد كشف التكرار الحالية.</summary>
    public async Task<OrganizationAliasSaveOutcome> CreateAsync(string? alias, string? canonical, string actor, CancellationToken ct)
    {
        var aliasKey = TextRules.OrganizationKey(alias ?? "");
        var canonicalKey = TextRules.OrganizationKey(canonical ?? "");
        if (aliasKey.Length == 0 || canonicalKey.Length == 0) return OrganizationAliasSaveOutcome.Empty;
        if (aliasKey == canonicalKey) return OrganizationAliasSaveOutcome.SameName;
        if (await db.OrganizationAliases.AnyAsync(x => x.Alias == aliasKey, ct)) return OrganizationAliasSaveOutcome.AlreadyExists;
        if (await db.OrganizationAliases.AnyAsync(x => x.Canonical == aliasKey, ct)) return OrganizationAliasSaveOutcome.ChainNotAllowed;
        var final = await db.OrganizationAliases.Where(x => x.Alias == canonicalKey)
            .Select(x => x.Canonical).FirstOrDefaultAsync(ct) ?? canonicalKey;
        db.OrganizationAliases.Add(new OrganizationAlias
        {
            Alias = aliasKey, Canonical = final, CreatedBy = actor.Truncate(256),
        });
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "OrganizationAlias.Create", Detail = $"{aliasKey} -> {final}".Truncate(2000) });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return OrganizationAliasSaveOutcome.SaveFailed; }
        return OrganizationAliasSaveOutcome.Added;
    }

    /// <summary>يحذف تعريف الاسم البديل فقط ويسجل القرار دون تعديل أسماء الفرص.</summary>
    public async Task<bool> DeleteAsync(int id, string actor, CancellationToken ct)
    {
        var alias = await db.OrganizationAliases.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (alias is null) return false;
        db.OrganizationAliases.Remove(alias);
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "OrganizationAlias.Delete", Detail = $"{alias.Alias} -> {alias.Canonical}".Truncate(2000) });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
