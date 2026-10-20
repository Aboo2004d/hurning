namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـProcessingStatus؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum ProcessingStatus
{
    Discovered = 0,
    AwaitingAi = 1,
    Retry = 2,
    Ready = 3,
    NeedsReview = 4,
    OutOfScope = 5,
    Published = 6,
}
