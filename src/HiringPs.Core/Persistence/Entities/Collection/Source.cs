// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>سجل Source وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public class Source
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public int Id { get; set; }

    /// <summary>الاسم المعروض للمستخدم أو الجهة المرتبطة.</summary>
    [Required, MaxLength(160)]
    public string Name { get; set; } = "";

    /// <summary>عنوان المورد الخارجي أو المنشور المرتبط بالسجل.</summary>
    [Required, MaxLength(1000)]
    public string Url { get; set; } = "";
    /// <summary>نوع موصل المصدر الذي يحدد طريقة اكتشاف محتواه.</summary>
    public ConnectorKind Connector { get; set; }
    /// <summary>يسمح باستخدام السجل أو التكامل؛ تعطيله لا يحذف البيانات السابقة.</summary>
    public bool Enabled { get; set; }
    /// <summary>موافقة الإدارة على استخدام المصدر وجمع محتواه.</summary>
    public bool UsageApproved { get; set; }

    /// <summary>سبب أو سند الموافقة على جمع المصدر.</summary>
    [MaxLength(2000)]
    public string ApprovalNote { get; set; } = "";
    /// <summary>إعدادات الموصل والمحددات المحفوظة بصيغة JSON.</summary>
    public string ConfigurationJson { get; set; } = "{}";
    /// <summary>الفاصل بين جولات جمع المصدر بالساعات.</summary>
    public int IntervalHours { get; set; } = 24;
    /// <summary>وقت بداية الجمع اليومي حسب إعدادات الجدولة.</summary>
    public TimeOnly CollectionStartTime { get; set; } = TimeOnly.MinValue;
    /// <summary>التاريخ المرجعي المستخدم لحساب جدول الجمع.</summary>
    public DateOnly? ScheduleAnchorDate { get; set; }
    /// <summary>أقرب موعد لإعادة محاولة جمع المصدر.</summary>
    public DateTimeOffset? CollectionRetryAt { get; set; }
    /// <summary>طلبات الجمع اليدوية التابعة للمصدر.</summary>
    public ICollection<SourceCollectionRequest> CollectionRequests { get; set; } = [];
    /// <summary>موعد جولة الجمع القادمة لهذا المصدر.</summary>
    public DateTimeOffset NextDueAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت آخر دورة نجحت.</summary>
    public DateTimeOffset? LastSuccessAt { get; set; }

    /// <summary>رمز آخر خطأ معالجة؛ لا يتضمن مفاتيح أو محتوى مزود حساسًا.</summary>
    [MaxLength(2000)]
    public string LastError { get; set; } = "";

    // Reset by a successful discovery; drives exponential retry backoff.
    /// <summary>عدد إخفاقات الجمع المتتالية لحساب التراجع الزمني؛ يصفر بعد نجاح الاكتشاف.</summary>
    public int ConsecutiveFailures { get; set; }

    // Collector health note for administrators (repeated failures or possible template change).
    /// <summary>تنبيه إداري عن تكرر الفشل أو احتمال تغير قالب المصدر.</summary>
    [MaxLength(500)]
    public string HealthWarning { get; set; } = "";
    /// <summary>وقت تسجيل آخر تحذير لصحة المصدر.</summary>
    public DateTimeOffset? HealthWarningAt { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
