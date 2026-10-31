namespace HiringPs.Core;
/// <summary>صف عرض لنتيجة موعد عشوائي مع عنوان الوظيفة.</summary>
public sealed record RandomPublicationRunDto(Guid Id, string Provider, DateTimeOffset ScheduledAt, long? OpportunityId,
    string Title, RandomPublicationState State, string ResultCode);
