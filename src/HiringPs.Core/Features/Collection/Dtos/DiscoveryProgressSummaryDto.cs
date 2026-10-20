namespace HiringPs.Core;

/// <summary>عدادات الاكتشاف المعروضة للإدارة دون مؤشر التنقل أو إعدادات الاستئناف.</summary>
public sealed record DiscoveryProgressSummaryDto(int SourceId, int CompletedPages, int Found);
