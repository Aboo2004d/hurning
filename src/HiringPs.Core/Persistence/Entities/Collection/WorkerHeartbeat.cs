// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>آخر نبضة حياة للعامل وحالته النصية المعروضة في لوحة الإدارة.</summary>
public class WorkerHeartbeat
{
    /// <summary>معرّف سجل نبضة العامل؛ تستخدم لوحة الإدارة أول سجل مرتبًا بهذا المعرّف.</summary>
    public int Id
    {
        get; set;
    }
    /// <summary>وقت آخر نبضة حياة محفوظة للعامل.</summary>
    public DateTimeOffset At
    {
        get; set;
    }
    /// <summary>رمز حالة العامل النصي المستخدم في ترجمة الحالة وعرضها؛ ليس enum مخزنًا.</summary>
    public string State { get; set; } = "";
}

// Single global row (Id = 1) for collector decisions shared across ticks and worker restarts.
