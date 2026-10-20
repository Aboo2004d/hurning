namespace HiringPs.Core;

/// <summary>مرشحات سجل العمليات؛ حدود التاريخ أيام بتوقيت فلسطين وتشمل اليوم الأخير كاملًا.</summary>
public sealed record OperationFilterDto(string? Search = null, OperationArea? Area = null,
    OperationOutcome? Outcome = null, DateOnly? From = null, DateOnly? To = null, string? Actor = null);
