namespace HiringPs.Core;

/// <summary>بيانات المشتري اللازمة لتهيئة الدفع فقط؛ لا تحمل كيان Identity أو ملف العضو.</summary>
/// <param name="Email">بريد المشتري الذي تتطلبه صفحة الدفع.</param>
/// <param name="Language">لغة العودة للموقع.</param>
public sealed record PaymentBuyerDto(string? Email, string Language);
