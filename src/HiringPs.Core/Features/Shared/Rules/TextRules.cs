// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>قواعد النص والبصمات وتاريخ فلسطين المشتركة بين المجالات.</summary>
public static class TextRules
{
    /// <summary>يحسب طول النص وفق طريقة العد المعتمدة في الموقع.</summary>
    public static int CharacterCount(string text) => new StringInfo(text).LengthInTextElements;

    /// <summary>يحسب بصمة ثابتة للبيانات دون الحاجة إلى حفظ القيمة السرية الأصلية.</summary>
    public static string Hash(string text) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))
        );

    /// <summary>ينشئ مفتاح مقارنة موحدًا لاسم الجهة.</summary>
    public static string OrganizationKey(string text)
    {
        var value = Normalize(text);
        return value.Length <= 200 ? value : "sha256:" + Hash(value);
    }

    /// <summary>يطبع النص للمقارنة والبحث مع الحفاظ على قواعد المجال الحالية.</summary>
    public static string Normalize(string text) =>
        string.Join(
            ' ',
            text.ToLowerInvariant()
                .Normalize()
                .Replace('أ', 'ا')
                .Replace('إ', 'ا')
                .Replace('آ', 'ا')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );

    private static readonly Lazy<TimeZoneInfo> PalestineZone = new(ResolvePalestineTimeZone);

    public static TimeZoneInfo PalestineTimeZone => PalestineZone.Value;

    /// <summary>يعيد تاريخ اليوم حسب توقيت فلسطين بدل توقيت الخادم.</summary>
    public static DateOnly PalestineToday() =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, PalestineTimeZone).DateTime
        );

    /// <summary>يحدد اسم المنطقة الزمنية الفلسطينية المتاح على نظام التشغيل.</summary>
    private static TimeZoneInfo ResolvePalestineTimeZone()
    {
        // Keep the project's existing IANA/Windows resolution order in one place.
        string[] candidates =
        [
            "Asia/Hebron",
            "Israel Standard Time",
            "Palestine Standard Time",
            "Jerusalem",
            "Middle East Standard Time",
        ];
        foreach (var id in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo
                .GetSystemTimeZones()
                .FirstOrDefault(zone =>
                    zone.Id.Contains("Jerusalem", StringComparison.OrdinalIgnoreCase)
                    || zone.Id.Contains("Israel", StringComparison.OrdinalIgnoreCase)
                    || zone.DisplayName.Contains("Jerusalem", StringComparison.OrdinalIgnoreCase)
                    || zone.DisplayName.Contains("Israel", StringComparison.OrdinalIgnoreCase)
                )
            ?? TimeZoneInfo.Utc;
    }
}
