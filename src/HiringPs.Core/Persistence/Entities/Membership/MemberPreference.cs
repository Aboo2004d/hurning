using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>تفضيلات العضو لأنواع الفرص وتصنيفاتها ومواقعها؛ تستخدم لتصفية تنبيهاته.</summary>
public sealed class MemberPreference
{
    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [Key, MaxLength(450)]
    public string UserId { get; set; } = "";
    /// <summary>قناع بتات أنواع الفرص المختارة ضمن تفضيلات العضو.</summary>
    public int Kinds { get; set; } = 15;

    /// <summary>قائمة التصنيفات المختارة بصيغة JSON.</summary>
    public string CategoriesJson { get; set; } = "[]";

    /// <summary>قائمة المواقع المختارة بصيغة JSON.</summary>
    public string LocationsJson { get; set; } = "[]";
    /// <summary>وقت آخر تعديل على تفضيلات العضو.</summary>
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
