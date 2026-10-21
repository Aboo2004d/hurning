namespace HiringPs.Core;

/// <summary>بيانات قائمة الأخطاء دون تحميل تفاصيل الاستثناء الكبيرة.</summary>
public sealed record SystemErrorSummaryDto(long Id, DateTimeOffset At, string Application,
    string Level, string Category, string TraceId, string Path, string ExceptionType, string Message);
