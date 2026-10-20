using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>سياسة الاحتفاظ بنسخ المصادر وملفات التشخيص وملخص آخر عملية تنظيف.</summary>
public class RetentionPolicy
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    [Key]
    public int Id { get; set; } = 1;
    /// <summary>يسمح باستخدام السجل أو التكامل؛ تعطيله لا يحذف البيانات السابقة.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>مدة الاحتفاظ بنسخ محتوى المصادر بالأيام.</summary>
    [Range(7, 3650)]
    public int RevisionDays { get; set; } = 90;

    /// <summary>مدة الاحتفاظ بملفات التشخيص بالأيام.</summary>
    [Range(7, 3650)]
    public int DiagnosticDays { get; set; } = 30;
    /// <summary>وقت آخر عملية تنظيف للبيانات القديمة.</summary>
    public DateTimeOffset? LastCleanupAt { get; set; }
    /// <summary>عدد النسخ التي حذفت في آخر تنظيف.</summary>
    public int LastDeletedRevisions { get; set; }
    /// <summary>عدد ملفات التشخيص التي حذفت في آخر تنظيف.</summary>
    public int LastDeletedDiagnostics { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
