// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>سجل DuplicateCandidate وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public class DuplicateCandidate
{
    /// <summary>المعرف الأساسي للسجل.</summary>
    public long Id { get; set; }
    /// <summary>معرف الفرصة المرتبطة بالسجل.</summary>
    public long OpportunityId { get; set; }
    /// <summary>الفرصة التي يخصها السجل.</summary>
    public Opportunity Opportunity { get; set; } = null!;
    /// <summary>معرف الفرصة المشابهة المقترحة هدفًا للدمج.</summary>
    public long CandidateId { get; set; }
    /// <summary>الفرصة المشابهة المرتبطة بمرشح التكرار.</summary>
    public Opportunity Candidate { get; set; } = null!;
    /// <summary>درجة تشابه العناوين المستخدمة لترشيح الزوج للمراجعة.</summary>
    public double Score { get; set; }

    /// <summary>سبب ترشيح التكرار لقرار الإدارة.</summary>
    [MaxLength(300)]
    public string Reason { get; set; } = "";
    /// <summary>حالة المعالجة أو قرار المراجعة المحفوظ.</summary>
    public DuplicateCandidateStatus Status { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت حسم مرشح التكرار بالدمج أو الاستبعاد.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>هوية المسؤول الذي حسم مرشح التكرار.</summary>
    [MaxLength(256)]
    public string ResolvedBy { get; set; } = "";
    /// <summary>معرف عملية الدمج القابلة للتراجع عند وجودها.</summary>
    public long? MergeOperationId { get; set; }
}
