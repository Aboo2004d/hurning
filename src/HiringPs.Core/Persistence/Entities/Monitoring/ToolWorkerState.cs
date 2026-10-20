using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>آخر حالة ونبضة حياة لإحدى نسخ العامل.</summary>
public class ToolWorkerState
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    [Key, MaxLength(32)]
    public string Id { get; set; } = "";

    /// <summary>اسم الجهاز الذي يشغّل العامل.</summary>
    [MaxLength(200)]
    public string Host { get; set; } = "";

    /// <summary>اسم حساب نظام التشغيل الذي يشغّل العامل.</summary>
    [MaxLength(200)]
    public string Account { get; set; } = "";
    /// <summary>وقت بدء تنفيذ العملية.</summary>
    public DateTimeOffset StartedAt { get; set; }
    /// <summary>وقت آخر نبضة حياة محفوظة للعامل.</summary>
    public DateTimeOffset At { get; set; }
    /// <summary>يشير إلى وجود جولة جمع قيد التنفيذ.</summary>
    public bool Collecting { get; set; }
    /// <summary>وقت آخر دورة معالجة للعامل.</summary>
    public DateTimeOffset? LastLoopAt { get; set; }
    /// <summary>وقت آخر دورة نجحت.</summary>
    public DateTimeOffset? LastSuccessAt { get; set; }
    /// <summary>وقت آخر دورة فشلت.</summary>
    public DateTimeOffset? LastFailureAt { get; set; }

    /// <summary>اسم نوع الخطأ التشخيصي دون نص استثناء قد يتضمن بيانات حساسة.</summary>
    [MaxLength(100)]
    public string ErrorType { get; set; } = "";
}
