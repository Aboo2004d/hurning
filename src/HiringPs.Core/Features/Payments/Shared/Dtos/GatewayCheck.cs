using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>DTO لنتيجة التحقق لدى البوابة يتضمن بيانات المطابقة ولا يمنح الاشتراك من تلقاء نفسه.</summary>
/// <param name="Confirmed">تأكيد المحول لوجود إيصال مطابق؛ تراجع خدمة التسوية بياناته قبل منح الاشتراك.</param>
/// <param name="OrderId">معرّف طلب الموقع الذي جرى التحقق منه.</param>
/// <param name="Reference">مرجع العملية لدى المزود، إن توفر.</param>
/// <param name="Amount">المبلغ المؤكد لدى المزود وليس قيمة افتراضية من الطلب.</param>
/// <param name="Currency">عملة الإيصال التي يجب مطابقتها بالطلب.</param>
/// <param name="State">حالة الدفع المفسرة من استجابة المزود.</param>
/// <param name="Inconclusive">يشير إلى عدم كفاية الدليل لتأكيد النجاح أو الفشل.</param>
/// <param name="DiagnosticCode">رمز تشخيص آمن دون مفاتيح أو نص استجابة المزود.</param>
public sealed record GatewayCheck(
    bool Confirmed,
    Guid OrderId,
    string? Reference,
    decimal? Amount,
    string? Currency,
    PaymentState State,
    bool Inconclusive = false,
    string? DiagnosticCode = null
);
