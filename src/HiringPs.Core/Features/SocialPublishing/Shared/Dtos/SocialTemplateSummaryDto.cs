namespace HiringPs.Core;

/// <summary>نسخة قالب للعرض وسجل الاستعادة دون كيان EF؛ خصائصها تسمح بتركيب مرشحات SQL قبل الترقيم.</summary>
public sealed record SocialTemplateSummaryDto
{
    public long Id { get; init; }
    public string Provider { get; init; } = "";
    public string Scope { get; init; } = "";
    public int Number { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string Actor { get; init; } = "";
    public string Body { get; init; } = "";
    public bool Active { get; init; }
}
