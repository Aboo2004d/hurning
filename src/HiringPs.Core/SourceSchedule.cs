namespace HiringPs.Core;

public static class SourceSchedule
{
    public static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");
    public static DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);
    public static DateTimeOffset NextAfter(Source source, DateTimeOffset after)
    {
        if (source.IntervalHours is < 1 or > 168) throw new InvalidOperationException("Collection interval must be 1–168 hours.");
        var date = source.ScheduleAnchorDate ?? LocalDate(after);
        var anchor = date.ToDateTime(source.CollectionStartTime, DateTimeKind.Unspecified);
        var localAfter = TimeZoneInfo.ConvertTime(after, Zone).DateTime;
        var elapsed = (localAfter - anchor).TotalHours;
        var step = Math.Max(0, (long)Math.Floor(elapsed / source.IntervalHours));
        for (var i = 0; i < 4; i++, step++)
        {
            var next = anchor.AddHours(step * source.IntervalHours);
            // Shift a nonexistent local clock time to the first valid minute on the DST transition.
            for (var m = 0; Zone.IsInvalidTime(next) && m < 180; m++) next = next.AddMinutes(1);
            var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(next, Zone), TimeSpan.Zero);
            if (utc > after) return utc;
        }
        throw new InvalidOperationException("Unable to compute next local collection slot.");
    }
    public static string Display(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).ToString("yyyy-MM-dd HH:mm");
}
