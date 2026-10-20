namespace HiringPs.Core;

/// <summary>نبضة حياة مختصرة للعامل كما يحفظها مسار الجمع الحالي.</summary>
public class WorkerHeartbeat
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public int Id { get; set; }
    /// <summary>وقت آخر نبضة حياة محفوظة للعامل.</summary>
    public DateTimeOffset At { get; set; }
    /// <summary>الحالة الحالية للعملية أو نتيجة الفحص؛ القيم معرفة في enum الخاص بالكيان.</summary>
    public string State { get; set; } = "";
}
