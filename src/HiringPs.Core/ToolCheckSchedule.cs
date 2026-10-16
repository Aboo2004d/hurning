namespace HiringPs.Core;

public static class ToolCheckSchedule
{
    // Reuse the application's Palestine timezone and daylight-saving handling.
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
