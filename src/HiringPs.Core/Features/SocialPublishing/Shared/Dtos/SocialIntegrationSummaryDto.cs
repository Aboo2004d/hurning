namespace HiringPs.Core;

/// <summary>حالة ربط منصة للإدارة دون رمز الوصول أو بيانات الحماية.</summary>
public sealed record SocialIntegrationSummaryDto(string Provider, string PageId, string PageName,
    bool Enabled, DateTimeOffset? ExpiresAt, DateTimeOffset ConnectedAt);
