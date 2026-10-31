namespace HiringPs.Core;

/// <summary>رد صالح للقراءة لكنه ناقص أو يخلط الحقول؛ لا يعد نجاحًا لاستخراج البيانات.</summary>
public sealed class AiExtractionRejectedException(string reason) : ContentNeedsReviewException(reason)
{
    public string Reason { get; } = reason;
    public Guid? DiagnosticId { get; set; }
    public override string Message => Reason + (DiagnosticId is { } id ? $" AI response diagnostic: {id:N}." : "");
}
