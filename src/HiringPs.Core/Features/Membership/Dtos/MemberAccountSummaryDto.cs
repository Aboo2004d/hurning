namespace HiringPs.Core;

/// <summary>حالة وبيانات حساب العضو اللازمة للواجهة؛ لا تكشف معرف مستخدم أو محادثة تليجرام.</summary>
public sealed record MemberAccountSummaryDto
{
    public string UserId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Mobile { get; init; } = "";
    public bool Activated
    {
        get; init;
    }
    public bool Suspended
    {
        get; init;
    }
    public string SuspensionReason { get; init; } = "";
    public bool TelegramLinked
    {
        get; init;
    }
    public bool NotificationsEnabled
    {
        get; init;
    }
    public bool SavedRemindersEnabled
    {
        get; init;
    }
}
