using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>طلب جمع يدوي دائم لمصدر بعينه، منفصل عن جدول الجمع التلقائي.</summary>
public class SourceCollectionRequest
{
    /// <summary>المفتاح الخارجي للمصدر الذي ينتمي إليه السجل.</summary>
    [
        Key,
        System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(
            System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None
        )
    ]
    /// <summary>بيانات SourceId المحفوظة للسجل.</summary>
    public int SourceId { get; set; }

    /// <summary>المصدر المرتبط بالسجل.</summary>
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(SourceId))]
    [System.ComponentModel.DataAnnotations.Schema.InverseProperty(
        nameof(HiringPs.Core.Source.CollectionRequests)
    )]
    /// <summary>بيانات Source المحفوظة للسجل.</summary>
    public Source Source { get; set; } = null!;
    /// <summary>مفتاح يميز دفعة طلب الجمع اليدوي.</summary>
    public Guid RequestToken { get; set; } = Guid.NewGuid();
    /// <summary>وقت إدراج الطلب في الطابور.</summary>
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت بدء تنفيذ العملية.</summary>
    public DateTimeOffset? StartedAt { get; set; }
    /// <summary>وقت انتهاء تنفيذ العملية.</summary>
    public DateTimeOffset? FinishedAt { get; set; }
    /// <summary>يشير إلى اكتمال مرحلة اكتشاف عناصر المصدر.</summary>
    public bool DiscoveryFinished { get; set; }

    /// <summary>حالة السجل ضمن دورة العمل؛ يستخدم النوع الحالي كما هو محفوظ في القاعدة.</summary>
    [MaxLength(40)]
    public string Status { get; set; } = "Pending";

    /// <summary>ملخص خطأ الجولة وفق قيود السجلات الحالية.</summary>
    [MaxLength(1000)]
    public string Error { get; set; } = "";

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
