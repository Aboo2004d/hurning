namespace HiringPs.Core;

/// <summary>صلاحيات عرض إجراءات الدفع؛ يجب أن يعيد كل أمر التحقق من الملكية والحالة على الخادم.</summary>
public sealed record PaymentActionOptions(bool CanPay, bool CanCheck, bool CanReport, bool CanRetry);
