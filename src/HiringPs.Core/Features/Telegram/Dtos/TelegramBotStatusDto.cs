namespace HiringPs.Core;

/// <summary>تفاصيل ربط البوت المسموح بعرضها للمسؤول، دون رمز البوت أو سر التحقق.</summary>
public sealed record TelegramBotStatusDto
{
    public TelegramBotSetupState State { get; init; }
    public bool Configured { get; init; }
    public string BotUsername { get; init; } = "";
    public string ExpectedWebhook { get; init; } = "";
    public string CurrentWebhook { get; init; } = "";
    public long? PendingUpdates { get; init; }
    public DateTimeOffset? LastErrorAt { get; init; }
    public string LastError { get; init; } = "";
    public string FailureCode { get; init; } = "";
    public string FailureDescription { get; init; } = "";
}
