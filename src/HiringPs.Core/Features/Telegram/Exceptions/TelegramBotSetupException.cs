namespace HiringPs.Core;

/// <summary>خطأ Bot API آمن؛ لا يحتفظ باستثناء HTTP الأصلي الذي قد يتضمن الرمز في عنوانه.</summary>
public sealed class TelegramBotSetupException(string code, string description) : Exception(description)
{
    public string Code { get; } = code;
}
