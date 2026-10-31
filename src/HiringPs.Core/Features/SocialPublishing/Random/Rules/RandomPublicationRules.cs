using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace HiringPs.Core;

/// <summary>سياسة اختيار الوظائف والتحقق من مواعيد النشر المحلية.</summary>
public static class RandomPublicationRules
{
    public static readonly string[] Providers = ["Facebook", "LinkedInPersonal", "LinkedIn"];
    /// <summary>يقرأ أوقات النسخة المحفوظة؛ الإعداد التالف لا يبدأ نشرًا.</summary>
    public static string[] Times(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }
    /// <summary>يتحقق من أوقات متزايدة بفاصل خمس دقائق على الأقل، بما فيها الانتقال لليوم التالي.</summary>
    public static bool Valid(int count, int days, string[]? times)
    {
        if (count is < 1 or > 24 || days is < 0 or > 3650 || times is null || times.Length != count) return false;
        var parsed = new List<TimeOnly>();
        foreach (var time in times)
        {
            if (!TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) return false;
            if (parsed.Count > 0 && value.ToTimeSpan() - parsed[^1].ToTimeSpan() < TimeSpan.FromMinutes(5)) return false;
            parsed.Add(value);
        }
        return count == 1 || TimeSpan.FromDays(1) - parsed[^1].ToTimeSpan() + parsed[0].ToTimeSpan() >= TimeSpan.FromMinutes(5);
    }
    /// <summary>يحول الموعد الفلسطيني إلى UTC؛ وقت تقديم الساعة غير الموجود لا ينشر تلقائيًا.</summary>
    public static DateTimeOffset? Instant(DateOnly day, string time)
    {
        if (!TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock)) return null;
        var local = day.ToDateTime(clock, DateTimeKind.Unspecified);
        return TextRules.PalestineTimeZone.IsInvalidTime(local) ? null
            : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TextRules.PalestineTimeZone));
    }
    /// <summary>يختار المنشور غير المغلق ضمن حد الأيام أو بلا نهاية، ويستبعد السجل اليدوي والعشوائي السابق.</summary>
    public static IQueryable<Opportunity> Candidates(HiringDb db, string provider, DateOnly today, int days)
    {
        var until = today.AddDays(days);
        return db.Opportunities.Where(o => o.Kind == OpportunityKind.Job && o.Status == PublicationStatus.Published
            && (o.ClosureEvidence == "")
            && (o.Deadline == null || (o.Deadline >= today && o.Deadline <= until))
            && !db.SocialPublications.Any(p => p.Provider == provider && p.OpportunityId == o.Id
                && (p.Status == "Published" || p.Status == "Pending" || p.Status == "Unknown"))
            && !db.Set<RandomPublicationRun>().Any(r => r.Provider == provider && r.OpportunityId == o.Id));
    }
}
