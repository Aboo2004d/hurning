namespace HiringPs.Core;

/// <summary>بيانات مراجعة الدفع المعروضة بعد التحقق من صلاحية صاحب الطلب أو الإدارة.</summary>
public sealed record PaymentReviewSummaryDto(string Note, string Resolution, DateTimeOffset? ResolvedAt, string? AttachmentName);
