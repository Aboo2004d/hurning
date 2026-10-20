namespace HiringPs.Core;

/// <summary>صف عرض المصدر دون إعدادات المتصفح أو روابط تتبع EF.</summary>
public sealed record SourceSummaryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public bool Enabled { get; init; }
    public bool UsageApproved { get; init; }
    public TimeOnly CollectionStartTime { get; init; }
    public int IntervalHours { get; init; }
    public DateTimeOffset NextDueAt { get; init; }
    public string LastError { get; init; } = "";
    public List<SourceCollectionRequestSummaryDto> CollectionRequests { get; init; } = [];
}
