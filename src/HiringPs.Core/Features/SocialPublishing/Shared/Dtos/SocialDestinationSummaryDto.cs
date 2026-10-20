namespace HiringPs.Core;

/// <summary>بيانات عرض وجهة النشر دون رمز وصول أو معلومات تشفير.</summary>
public sealed record SocialDestinationSummaryDto(string Provider, string PageName, bool Enabled, DateTimeOffset? ExpiresAt);
