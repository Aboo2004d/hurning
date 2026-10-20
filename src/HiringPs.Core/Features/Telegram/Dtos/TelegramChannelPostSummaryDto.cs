namespace HiringPs.Core;

/// <summary>صف سجل توزيع القنوات دون نص الرسالة أو رمز البوت أو كيان فرصة كامل.</summary>
public sealed record TelegramChannelPostSummaryDto
{
    public Guid Id { get; init; }
    public long OpportunityId { get; init; }
    public SocialOpportunitySummaryDto Opportunity { get; init; } = null!;
    public string PageId { get; init; } = "";
    public string Status { get; init; } = "";
    public string Error { get; init; } = "";
    public int Attempts { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? NextAttemptAt { get; init; }
    public string Url { get; init; } = "";
}
