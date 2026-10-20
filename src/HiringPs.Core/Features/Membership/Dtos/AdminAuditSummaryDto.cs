namespace HiringPs.Core;

/// <summary>صف سجل إداري للعرض دون كيان قابل للتتبع.</summary>
public sealed record AdminAuditSummaryDto(DateTimeOffset At, string Actor, string Action, string Detail);
