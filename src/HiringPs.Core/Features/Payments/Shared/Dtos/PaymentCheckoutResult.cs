namespace HiringPs.Core;

/// <summary>نتيجة بدء الدفع دون الاعتماد على MVC أو تنفيذ redirect داخل الخدمة.</summary>
/// <param name="Route">نوع التنقل المطلوب من الواجهة.</param>
/// <param name="Notice">رمز الرسالة المعتمد للترجمة، إن وجد.</param>
/// <param name="Url">رابط المزود الذي اجتاز التحقق عند اختيار التنقل الخارجي فقط.</param>
public sealed record PaymentCheckoutResult(PaymentCheckoutRoute Route, string? Notice = null, string? Url = null);
