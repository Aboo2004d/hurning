using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>مجموعة مجالات اعتمدها المسؤول؛ إنشاؤها أو تعديلها وحده يمنح التكافؤ للتنبيهات.</summary>
[Index(nameof(NormalizedName), IsUnique = true)]
public sealed class CategoryGroup
{
    /// <summary>معرف المجموعة المستخدم في العد والمطابقة.</summary>
    public int Id { get; set; }
    /// <summary>اسم العرض الذي اختاره المسؤول.</summary>
    [MaxLength(160)] public string Name { get; set; } = "";
    /// <summary>اسم موحد لمنع تكرار أسماء المجموعات.</summary>
    [MaxLength(200)] public string NormalizedName { get; set; } = "";
    /// <summary>وقت الاعتماد الأول UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت آخر تعديل UTC.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>الأسماء الأصلية التي وافق المسؤول على تكافئها.</summary>
    public List<CategoryGroupMember> Members { get; set; } = [];
    /// <summary>حماية التعديلات المتزامنة.</summary>
    [Timestamp] public byte[] Version { get; set; } = [];
}
