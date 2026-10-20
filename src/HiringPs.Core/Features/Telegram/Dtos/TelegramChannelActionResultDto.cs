namespace HiringPs.Core;

/// <summary>نتيجة إجراء القناة ورمز رسالته المترجم.</summary>
public sealed record TelegramChannelActionResultDto(TelegramChannelActionOutcome Outcome, string? Notice = null);
