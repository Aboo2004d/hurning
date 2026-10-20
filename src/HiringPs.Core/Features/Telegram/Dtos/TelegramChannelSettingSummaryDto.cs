namespace HiringPs.Core;

/// <summary>حالة تفعيل قناة وتاريخ البدء دون كيان تخزين.</summary>
public sealed record TelegramChannelSettingSummaryDto(string Target, bool Enabled, DateTimeOffset? EnabledAt);
