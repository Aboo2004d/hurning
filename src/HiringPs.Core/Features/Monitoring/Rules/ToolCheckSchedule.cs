namespace HiringPs.Core;

/// <summary>يحسب موعد الفحص التلقائي اليومي وفق توقيت فلسطين.</summary>
public static class ToolCheckSchedule
{
    // Reuse the application's Palestine timezone and daylight-saving handling.
    /// <summary>يحسب موعد الفحص اليومي القادم حسب توقيت فلسطين.</summary>
    public static DateTimeOffset NextMidnight(DateTimeOffset after) =>
        SourceSchedule.NextAfter(
            new Source
            {
                IntervalHours = 24,
                CollectionStartTime = TimeOnly.MinValue,
                ScheduleAnchorDate = SourceSchedule.LocalDate(after),
            },
            after
        );
}
