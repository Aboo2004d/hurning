using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة وحفظ تفضيلات عضو واحد، مع إبقاء صيغة JSON وقناع الأنواع المستخدمين في التنبيهات.</summary>
public sealed class MemberPreferenceService(HiringDb db)
{
    /// <summary>يقرأ اختيارات العضو فقط؛ البيانات التاريخية غير الصالحة تحتفظ بسلوك الخطأ السابق.</summary>
    public async Task<MemberPreferenceDto> ReadAsync(string owner, CancellationToken ct)
    {
        var pref = await db.MemberPreferences.AsNoTracking().Where(x => x.UserId == owner)
            .Select(x => new { x.Kinds, x.CategoriesJson, x.LocationsJson }).SingleAsync(ct);
        return new(pref.Kinds, JsonSerializer.Deserialize<string[]>(pref.CategoriesJson) ?? [],
            JsonSerializer.Deserialize<string[]>(pref.LocationsJson) ?? []);
    }

    /// <summary>يحضر كتالوج التفضيلات بالقواعد السابقة للمنشور والمؤرشف والمواقع المعتمدة.</summary>
    public async Task<MemberPreferenceOptionsDto> OptionsAsync(CancellationToken ct)
    {
        var categories = await db.Opportunities.Where(x => x.Category != ""
            && (x.Status == PublicationStatus.Published || x.Status == PublicationStatus.Archived))
            .Select(x => x.Category).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        return new(categories, await LocationCatalog.ApprovedAsync(db, ct));
    }

    /// <summary>يحفظ الاختيارات وعلامة التذكيرات عند تقديمها في SaveChanges واحدة؛ لا يرسل تنبيهًا.</summary>
    public async Task SaveAsync(string owner, MemberPreferenceUpdateDto input, CancellationToken ct)
    {
        var pref = await db.MemberPreferences.SingleAsync(x => x.UserId == owner, ct);
        pref.Kinds = input.Kinds;
        pref.CategoriesJson = JsonSerializer.Serialize(input.Categories);
        pref.LocationsJson = JsonSerializer.Serialize(input.Locations);
        pref.ChangedAt = DateTimeOffset.UtcNow;
        if (input.SavedReminders is { } reminders)
        {
            var profile = await db.MemberProfiles.SingleAsync(x => x.UserId == owner, ct);
            profile.SavedRemindersEnabled = reminders;
        }
        await db.SaveChangesAsync(ct);
    }
}
