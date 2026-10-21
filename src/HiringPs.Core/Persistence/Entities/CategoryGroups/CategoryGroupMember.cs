using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>انتماء تسمية أصلية إلى مجموعة واحدة فقط؛ لا يغير مجال الفرص أو تفضيلات الأعضاء.</summary>
[Index(nameof(NormalizedKey), IsUnique = true)]
public sealed class CategoryGroupMember
{
    /// <summary>معرف سجل الانتماء.</summary>
    public int Id { get; set; }
    /// <summary>المجموعة المعتمدة المرتبطة.</summary>
    public int CategoryGroupId { get; set; }
    /// <summary>مرجع المجموعة، مع حذف الانتماء عند إزالة المجموعة فقط.</summary>
    public CategoryGroup CategoryGroup { get; set; } = null!;
    /// <summary>اسم المجال الأصلي دون اختصار.</summary>
    public string Label { get; set; } = "";
    /// <summary>مفتاح تطابق تام موحد، أو بصمة للمسميات الطويلة.</summary>
    [MaxLength(200)] public string NormalizedKey { get; set; } = "";
}
