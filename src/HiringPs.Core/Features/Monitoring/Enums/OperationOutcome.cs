namespace HiringPs.Core;

/// <summary>نتيجة حدث في سجل العمليات، مستقلة عن حالات كيانات المجال.</summary>
public enum OperationOutcome
{
    Started = 0,
    Succeeded = 1,
    Failed = 2,
    Pending = 3,
    Skipped = 4,
    Uncertain = 5,
    Unknown = 6,
}
