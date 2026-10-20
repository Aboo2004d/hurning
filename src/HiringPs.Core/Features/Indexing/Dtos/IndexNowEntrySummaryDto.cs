namespace HiringPs.Core;

/// <summary>حقول حالة إخطار الفهرسة المعروضة للإدارة دون تتبع الكيان.</summary>
public sealed record IndexNowEntrySummaryDto(long OpportunityId, DateTimeOffset? SubmittedAt,
    int? LastStatus, string LastError, int Attempts);
