namespace HiringPs.Core;

/// <summary>تفاصيل سجل خطأ واحد للعرض الإداري فقط.</summary>
public sealed record SystemErrorDetailsDto(SystemErrorSummaryDto Summary, string Details);
