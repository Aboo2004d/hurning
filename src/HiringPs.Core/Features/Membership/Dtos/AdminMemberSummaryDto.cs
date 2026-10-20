namespace HiringPs.Core;

/// <summary>بيانات عرض العضو للإدارة دون معرفات تليجرام الداخلية أو إعدادات التنبيه أو rowversion.</summary>
public sealed record AdminMemberSummaryDto
{
    public string UserId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Mobile { get; init; } = "";
    public bool Activated { get; init; }
    public bool Suspended { get; init; }
    public string SuspensionReason { get; init; } = "";
}
