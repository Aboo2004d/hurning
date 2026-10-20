// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>مكون TextRules ضمن مجال Shared؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class TextRules
{
    /// <summary>ينفذ CharacterCount وفق قواعد TextRules الحالية دون تغيير عقد المستدعين.</summary>
    public static int CharacterCount(string text) => new StringInfo(text).LengthInTextElements;

    /// <summary>ينفذ Hash وفق قواعد TextRules الحالية دون تغيير عقد المستدعين.</summary>
    public static string Hash(string text) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))
        );

    /// <summary>ينفذ OrganizationKey وفق قواعد TextRules الحالية دون تغيير عقد المستدعين.</summary>
    public static string OrganizationKey(string text)
    {
        var value = Normalize(text);
        return value.Length <= 200 ? value : "sha256:" + Hash(value);
    }

    /// <summary>ينفذ Normalize وفق قواعد TextRules الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ PalestineToday وفق قواعد TextRules الحالية دون تغيير عقد المستدعين.</summary>
    public static DateOnly PalestineToday() =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, PalestineTimeZone).DateTime
        );

    // Set when the server lacks a Palestinian zone; the worker logs it once.
    public static string? PalestineTimeZoneWarning { get; private set; }

    /// <summary>ينفذ ResolvePalestineTimeZone وفق قواعد TextRules الحالية دون تغيير عقد المستدعين.</summary>
    private static TimeZoneInfo ResolvePalestineTimeZone()
    {
        // Palestinian zones first (Windows id, then IANA); Israeli zones differ around DST changes.
        string[] palestinian = ["West Bank Standard Time", "Asia/Hebron", "Asia/Gaza"];
        string[] fallbacks =
        [
            "Israel Standard Time",
            "Palestine Standard Time",
            "Jerusalem",
            "Middle East Standard Time",
        ];
        foreach (var id in palestinian.Concat(fallbacks))
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                if (!palestinian.Contains(id))
                    Fallback($"Palestinian time zone not found; using {id}.");
                return zone;
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        var match = TimeZoneInfo
            .GetSystemTimeZones()
            .FirstOrDefault(zone =>
                zone.Id.Contains("Jerusalem", StringComparison.OrdinalIgnoreCase)
                || zone.Id.Contains("Israel", StringComparison.OrdinalIgnoreCase)
                || zone.DisplayName.Contains("Jerusalem", StringComparison.OrdinalIgnoreCase)
                || zone.DisplayName.Contains("Israel", StringComparison.OrdinalIgnoreCase)
            );
        Fallback(
            match is null
                ? "No Palestinian time zone found; using UTC."
                : $"Palestinian time zone not found; using {match.Id}."
        );
        return match ?? TimeZoneInfo.Utc;
    }

    /// <summary>ينفذ Fallback وفق قواعد TextRules الحالية دون تغيير عقد المستدعين.</summary>
    private static void Fallback(string warning)
    {
        PalestineTimeZoneWarning = warning;
        System.Diagnostics.Trace.TraceWarning(warning);
    }
}
