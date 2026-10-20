namespace HiringPs.Core;

/// <summary>نتيجة الدمج ومعرّفا الاتجاه الفعلي لرسالة الإدارة.</summary>
public sealed record DuplicateMergeResultDto(DuplicateActionOutcome Outcome, long DuplicateId = 0, long TargetId = 0);
