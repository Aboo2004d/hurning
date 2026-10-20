namespace HiringPs.Core;

/// <summary>حقول عرض حدث واحد دون تحميل كيانات المجال أو حمولات المزودين.</summary>
public sealed record OperationSummaryDto(long Id, DateTimeOffset At, string Actor, string Action, string Detail,
    OperationArea Area, OperationOutcome Outcome)
{
    /// <summary>منشئ الإسقاط الذي يسمح بترجمة مرشحات الخصائص إلى SQL قبل الترقيم.</summary>
    public OperationSummaryDto() : this(0, default, "", "", "", default, default) { }
}
