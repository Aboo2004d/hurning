namespace HiringPs.Core;

/// <summary>قرار معالجة إشعار لحظة قبل ترجمته إلى حالة HTTP في الواجهة.</summary>
public enum LahzaWebhookOutcome
{
    Accepted = 0,
    InvalidRequest = 1,
    Unauthorized = 2,
}
