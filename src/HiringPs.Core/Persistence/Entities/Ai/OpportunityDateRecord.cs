using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>موعد مرتبط بفرصة مع نوعه ودليله وبصمة الدليل لمنع تكراره.</summary>
public class OpportunityDateRecord
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    public long OpportunityId { get; set; }
    /// <summary>المفتاح الخارجي لعنصر المصدر المرتبط.</summary>
    public long SourceItemId { get; set; }
    /// <summary>معرّف نسخة تعليمات AI المستخدمة في هذه العملية.</summary>
    public int? PromptVersionId { get; set; }
    /// <summary>نوع الفرصة أو الفحص أو الرسالة وفق enum الخاص بالسجل.</summary>
    public OpportunityDateKind Kind { get; set; }
    /// <summary>التاريخ المستخلص الذي يمثله دليل الموعد.</summary>
    public DateOnly Date { get; set; }

    /// <summary>وصف الموعد كما ظهر في المصدر.</summary>
    [MaxLength(300)]
    public string Label { get; set; } = "";
    /// <summary>النص الداعم للاستنتاج أو الموعد.</summary>
    public string Evidence { get; set; } = "";

    /// <summary>بصمة الدليل لمنع تكرار حفظه.</summary>
    [MaxLength(64)]
    public string EvidenceHash { get; set; } = "";
    /// <summary>يشير إلى أن الموعد يمثل تمديدًا.</summary>
    public bool IsExtension { get; set; }
    /// <summary>وقت حفظ الدليل في القاعدة.</summary>
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>الفرصة المرتبطة التي يحمّلها EF Core عند الحاجة.</summary>
    public Opportunity Opportunity { get; set; } = null!;
}
