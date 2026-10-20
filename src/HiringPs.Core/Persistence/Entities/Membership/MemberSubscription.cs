using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>اشتراك عضو مرتبط بنسخة باقة وبيئة دفع محددتين.</summary>
public sealed class MemberSubscription
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }

    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [MaxLength(450)]
    public string UserId { get; set; } = "";
    /// <summary>المفتاح الخارجي لنسخة الباقة المرتبطة بالسجل.</summary>
    public int PlanVersionId { get; set; }
    /// <summary>نسخة الباقة المشتراة؛ تبقى شروطها محفوظة حتى لو تغيرت الباقة الحالية.</summary>
    public MembershipPlanVersion PlanVersion { get; set; } = null!;
    /// <summary>معرّف طلب الدفع المرتبط بالسجل.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>بيئة الدفع Sandbox أو Production؛ تمنع خلط المعاملات بين البيئتين.</summary>
    [MaxLength(12)]
    public string GatewayEnvironment { get; set; } = "Production";
    /// <summary>وقت بداية صلاحية الاشتراك.</summary>
    public DateTimeOffset StartsAt { get; set; }
    /// <summary>وقت انتهاء صلاحية الاشتراك.</summary>
    public DateTimeOffset EndsAt { get; set; }
    /// <summary>يشير إلى إلغاء الاشتراك.</summary>
    public bool Cancelled { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
