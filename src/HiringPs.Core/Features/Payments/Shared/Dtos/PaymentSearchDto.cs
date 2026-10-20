namespace HiringPs.Core;

/// <summary>مرشحات قراءة طلبات الدفع للإدارة دون ربطها بنوع طلب HTTP أو بكيان قابل للتعديل.</summary>
public sealed record PaymentSearchDto(string? Query, string? Gateway, string? Environment, PaymentState? State, bool Review, bool Unresolved = false);
