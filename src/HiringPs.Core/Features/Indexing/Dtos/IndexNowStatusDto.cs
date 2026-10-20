namespace HiringPs.Core;

/// <summary>ملخص طابور IndexNow وآخر الإخطارات دون المفتاح السري أو طلبات HTTP.</summary>
public sealed record IndexNowStatusDto(int Pending, int Failing, DateTimeOffset? LastSubmission,
    List<IndexNowEntrySummaryDto> Recent);
