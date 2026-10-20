using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>طلب فحص دائم ينفذه العامل ويحرر حجزه عند الانتهاء.</summary>
public class ToolCheckRequest
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>نوع الفرصة أو الفحص أو الرسالة وفق enum الخاص بالسجل.</summary>
    public ToolCheckKind Kind { get; set; }
    /// <summary>حالة السجل ضمن دورة العمل؛ يستخدم النوع الحالي كما هو محفوظ في القاعدة.</summary>
    public ToolRequestStatus Status { get; set; }
    /// <summary>حجز فريد لنوع الفحص أثناء الانتظار والتنفيذ؛ يحرر عند انتهاء الطلب.</summary>
    public int? ActiveSlot { get; set; }

    /// <summary>هوية المستخدم أو العامل الذي نفذ العملية.</summary>
    [MaxLength(200)]
    public string Actor { get; set; } = "";

    /// <summary>معرّف نسخة العامل التي نفذت الطلب أو الفحص.</summary>
    [MaxLength(32)]
    public string WorkerId { get; set; } = "";

    /// <summary>رمز النتيجة النهائية للعملية.</summary>
    [MaxLength(80)]
    public string Outcome { get; set; } = "";
    /// <summary>وقت إدراج الطلب في الطابور.</summary>
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت بدء تنفيذ العملية.</summary>
    public DateTimeOffset? StartedAt { get; set; }
    /// <summary>وقت انتهاء تنفيذ العملية.</summary>
    public DateTimeOffset? FinishedAt { get; set; }
}
