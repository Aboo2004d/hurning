using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

// A single session per order. ConfirmationStartedAt is a permanent, durable claim:
// the partner explicitly forbids a second confirm call, even after an error.
/// <summary>جلسة تأكيد محفظة مرتبطة بطلب دفع؛ تحفظ حجز الخصم ونتيجته دون OTP.</summary>
public sealed class WalletPaymentSession
{
    /// <summary>معرّف طلب الدفع المرتبط بالسجل.</summary>
    [Key]
    public Guid OrderId { get; set; }
    /// <summary>طلب الدفع الذي تتبعه جلسة المحفظة.</summary>
    public PaymentOrder Order { get; set; } = null!;

    /// <summary>بيئة الدفع Sandbox أو Production؛ تمنع خلط المعاملات بين البيئتين.</summary>
    [MaxLength(12)]
    public string GatewayEnvironment { get; set; } = "Production";

    /// <summary>رقم المحفظة المطلوب الخصم منها؛ بيانات شخصية لا تُسجل في التشخيص.</summary>
    [MaxLength(10)]
    public string Phone { get; set; } = "";

    /// <summary>مفتاح جلسة المحفظة مشفر؛ يفك فقط عند تنفيذ تأكيد الدفع.</summary>
    [MaxLength(4096)]
    public string? ProtectedSessionKey { get; set; }

    /// <summary>بصمة مفتاح الجلسة لاكتشاف إعادة الاستخدام دون تخزين المفتاح مكشوفًا.</summary>
    [MaxLength(64)]
    public string? SessionKeyHash { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>وقت انتهاء صلاحية السجل أو التفويض؛ لا يقبل الاستخدام بعده.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>يشير إلى رفض صريح لطلب بدء جلسة المحفظة.</summary>
    public bool InitiationRejected { get; set; }
    /// <summary>وقت حجز طلب الخصم؛ يمنع تكرار إرسال أمر التأكيد.</summary>
    public DateTimeOffset? ConfirmationStartedAt { get; set; }
    /// <summary>يشير إلى رفض صريح لطلب تأكيد المحفظة.</summary>
    public bool ConfirmationRejected { get; set; }
    /// <summary>معرّف المعاملة المؤكدة لدى مزود المحفظة.</summary>
    public long? TransactionId { get; set; }

    /// <summary>المرجع النصي الذي أعاده مزود المحفظة.</summary>
    [MaxLength(200)]
    public string? JawwalReference { get; set; }

    /// <summary>المبلغ الإجمالي الذي أكدته المحفظة.</summary>
    [Precision(18, 2)]
    public decimal? ConfirmedAmount { get; set; }

    /// <summary>رسوم مزود المحفظة كما وردت في الإيصال.</summary>
    [Precision(18, 2)]
    public decimal? JawwalFee { get; set; }

    /// <summary>رسوم المنصة كما وردت في الإيصال.</summary>
    [Precision(18, 2)]
    public decimal? PlatformFee { get; set; }

    /// <summary>صافي المبلغ المقيد بعد خصم الرسوم.</summary>
    [Precision(18, 2)]
    public decimal? NetCredited { get; set; }
    /// <summary>وقت حفظ التأكيد الصادر عن مزود المحفظة.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
