namespace HiringPs.Core;

/// <summary>قواعد تحديد مواعيد جولات المصدر حسب توقيت فلسطين والفاصل المضبوط.</summary>
public static class SourceSchedule
{
    public static TimeZoneInfo Zone => TextRules.PalestineTimeZone;

    /// <summary>يحول اللحظة إلى تاريخ فلسطين المستخدم في جدولة المصادر.</summary>
    public static DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>يحسب موعد الجمع التالي بعد اللحظة المحددة وفق جدول المصدر.</summary>
    public static DateTimeOffset NextAfter(Source source, DateTimeOffset after)
    {
        if (source.IntervalHours is < 1 or > 168)
            throw new InvalidOperationException("Collection interval must be 1–168 hours.");
        var date = source.ScheduleAnchorDate ?? LocalDate(after);
        var anchor = date.ToDateTime(source.CollectionStartTime, DateTimeKind.Unspecified);
        var localAfter = TimeZoneInfo.ConvertTime(after, Zone).DateTime;
        var elapsed = (localAfter - anchor).TotalHours;
        var step = Math.Max(0, (long)Math.Floor(elapsed / source.IntervalHours));
        for (var i = 0; i < 4; i++, step++)
        {
            var next = anchor.AddHours(step * source.IntervalHours);
            // Shift a nonexistent local clock time to the first valid minute on the DST transition.
            for (var m = 0; Zone.IsInvalidTime(next) && m < 180; m++)
                next = next.AddMinutes(1);
            var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(next, Zone), TimeSpan.Zero);
            if (utc > after)
                return utc;
        }

        throw new InvalidOperationException("Unable to compute next local collection slot.");
    }

    /// <summary>ينشئ وصفًا معروضًا للقيمة وفق لغة المستخدم وقواعد المجال.</summary>
    public static string Display(DateTimeOffset instant) => Display(instant, seconds: false);

    /// <summary>ينشئ وصفًا معروضًا للقيمة وفق لغة المستخدم وقواعد المجال.</summary>
    public static string Display(DateTimeOffset instant, bool seconds) =>
        TimeZoneInfo
            .ConvertTime(instant, Zone)
            .ToString(
                seconds ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm",
                System.Globalization.CultureInfo.InvariantCulture
            );
}
