// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>سجل WorkerHeartbeat وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public class WorkerHeartbeat
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public int Id { get; set; }
    /// <summary>وقت آخر نبضة حياة محفوظة للعامل.</summary>
    public DateTimeOffset At { get; set; }
    /// <summary>الحالة الحالية للعملية أو نتيجة الفحص؛ القيم معرفة في enum الخاص بالكيان.</summary>
    public string State { get; set; } = "";
}

// Single global row (Id = 1) for collector decisions shared across ticks and worker restarts.
