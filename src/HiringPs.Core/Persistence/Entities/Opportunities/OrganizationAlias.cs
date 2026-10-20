// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>سجل OrganizationAlias وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public class OrganizationAlias
{
    /// <summary>المعرف الأساسي للسجل.</summary>
    public int Id { get; set; }

    /// <summary>صيغة اسم الجهة البديلة بعد التطبيع.</summary>
    [Required, MaxLength(200)]
    public string Alias { get; set; } = "";

    /// <summary>المفتاح النهائي المعتمد للجهة لمطابقة التكرارات دون تغيير الاسم المعروض.</summary>
    [Required, MaxLength(200)]
    public string Canonical { get; set; } = "";

    /// <summary>هوية المسؤول الذي أضاف السجل.</summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
