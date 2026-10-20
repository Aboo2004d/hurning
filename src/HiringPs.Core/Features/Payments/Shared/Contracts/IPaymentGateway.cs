namespace HiringPs.Core;

// Only an adapter backed by the actual provider contract may report Ready.
// No guessed endpoints, fake successful payments or browser-return confirmations.
/// <summary>عقد بوابة الدفع الذي يفصل تهيئة الدفع والتحقق منه عن منح الاشتراك وإدارة الطلب.</summary>
public interface IPaymentGateway
{
    /// <summary>المفتاح الثابت الذي يربط المحول بإعدادات البوابة وسجلات الدفع.</summary>
    string Key { get; }
    /// <summary>جاهزية المحول حسب إعداداته؛ لا تعني أن طلب دفع بعينه قد نجح.</summary>
    bool Ready { get; }
    /// <summary>بيئة اعتماد المحول لمنع تأكيد طلب Sandbox بمفاتيح Production أو العكس.</summary>
    string Environment => "Production";
    /// <summary>يتحقق من حدود المبلغ ودقته قبل طلب عملية من المزود.</summary>
    bool SupportsAmount(decimal amount) => amount > 0;
    /// <summary>ينشئ رابط دفع مستضافًا وفق عقد المزود؛ لا يمنح اشتراكًا عند العودة من المتصفح.</summary>
    Task<GatewayCheckout> CheckoutAsync(PaymentOrder order, CancellationToken ct);
    /// <summary>يقرأ دليل الدفع من المزود ويعيد DTO للمطابقة؛ لا يكرر أمر خصم أو يعتمد عودة المتصفح.</summary>
    Task<GatewayCheck> CheckAsync(PaymentOrder order, CancellationToken ct);
}
