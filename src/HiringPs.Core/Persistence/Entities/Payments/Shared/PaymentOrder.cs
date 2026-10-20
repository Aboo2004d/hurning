using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>طلب دفع يحتفظ بالمبلغ والبيئة وشروط الشراء وقت إنشائه.</summary>
public sealed class PaymentOrder
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [MaxLength(450)]
    public string UserId { get; set; } = "";
    /// <summary>المفتاح الخارجي لنسخة الباقة المرتبطة بالسجل.</summary>
    public int PlanVersionId { get; set; }
    /// <summary>نسخة الباقة المشتراة؛ تبقى شروطها محفوظة حتى لو تغيرت الباقة الحالية.</summary>
    public MembershipPlanVersion PlanVersion { get; set; } = null!;
    /// <summary>معرّف كود الخصم المطبق، إن وجد.</summary>
    public int? DiscountCodeId { get; set; }
    /// <summary>نسبة الخصم التي حجزت عند إنشاء الطلب.</summary>
    public int DiscountPercent { get; set; }

    /// <summary>مبلغ الطلب بالشيكل؛ يقارن بإيصال المزود قبل اعتماد الدفع.</summary>
    [Precision(18, 2)]
    public decimal AmountIls { get; set; }

    /// <summary>رمز عملة العملية؛ لا يعتمد الدفع إذا خالفت العملة المتوقعة.</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "ILS";

    /// <summary>المفتاح المعتمد لمحول بوابة الدفع.</summary>
    [MaxLength(30)]
    public string Gateway { get; set; } = "";

    /// <summary>بيئة الدفع Sandbox أو Production؛ تمنع خلط المعاملات بين البيئتين.</summary>
    [MaxLength(12)]
    public string GatewayEnvironment { get; set; } = "Production";

    /// <summary>مرجع العملية لدى المزود؛ فريد ضمن البوابة وبيئة الدفع.</summary>
    [MaxLength(200)]
    public string? ProviderReference { get; set; }

    /// <summary>رابط صفحة الدفع المستضافة لدى المزود.</summary>
    [MaxLength(2048)]
    public string? CheckoutUrl { get; set; }
    /// <summary>وقت حجز بدء صفحة الدفع لمنع إنشائها أكثر من مرة.</summary>
    public DateTimeOffset? CheckoutStartedAt { get; set; }
    /// <summary>الحالة الحالية للعملية أو نتيجة الفحص؛ القيم معرفة في enum الخاص بالكيان.</summary>
    public PaymentState State { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت انتهاء صلاحية السجل أو التفويض؛ لا يقبل الاستخدام بعده.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>وقت تأكيد استلام الدفعة.</summary>
    public DateTimeOffset? PaidAt { get; set; }
    /// <summary>وقت آخر تحقق؛ يستخدم أيضًا للحد من تكرار استدعاء مزود الدفع.</summary>
    public DateTimeOffset? LastCheckedAt { get; set; }

    /// <summary>معرّف شروط الخدمة التي وافق عليها صاحب الطلب.</summary>
    [MaxLength(100)]
    public string TermsVersion { get; set; } = "2026-10-18-no-refunds";
    /// <summary>وقت موافقة صاحب الطلب على الشروط.</summary>
    public DateTimeOffset TermsAcceptedAt { get; set; }

    /// <summary>سبب اعتماد قرار الدفع أو رفضه.</summary>
    [MaxLength(2000)]
    public string DecisionReason { get; set; } = "";

    /// <summary>دليل الدفع المحفوظ للمراجعة الإدارية.</summary>
    [MaxLength(2000)]
    public string PaymentEvidence { get; set; } = "";

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
