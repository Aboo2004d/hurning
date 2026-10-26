namespace HiringPs.Core;

/// <summary>حالة إعداد البوت وتسجيل استقبال تحديثاته؛ لا تثبت نجاح تسليم التنبيهات.</summary>
public enum TelegramBotSetupState
{
    Disabled, InvalidConfiguration, Unavailable, IdentityMismatch, NotRegistered, DifferentWebhook, UpdatesDisabled, Registered
}
