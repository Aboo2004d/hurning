namespace HiringPs.Core;

/// <summary>أمر اعتماد إداري بسبب ودليل إلزاميين؛ تؤخذ هوية المنفذ من جلسة الإدارة.</summary>
public sealed record ManualPaymentApprovalDto(Guid OrderId, string Actor, string? Reason, string? Evidence);
