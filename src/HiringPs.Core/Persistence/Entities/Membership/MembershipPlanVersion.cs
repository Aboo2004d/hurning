using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

// Purchased versions are immutable. Editing creates a new version.
/// <summary>نسخة غير قابلة للتعديل من شروط الباقة؛ المشتريات ترتبط بها لا بالسعر الحالي.</summary>
public sealed class MembershipPlanVersion
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public int Id { get; set; }
    /// <summary>معرّف الباقة؛ القيمة الفارغة في الخصومات تعني أن الكود يشمل جميع الباقات.</summary>
    public int PlanId { get; set; }

    /// <summary>الاسم المعروض في الواجهة العربية.</summary>
    [MaxLength(160)]
    public string NameAr { get; set; } = "";

    /// <summary>الاسم المعروض في الواجهة الإنجليزية.</summary>
    [MaxLength(160)]
    public string NameEn { get; set; } = "";

    /// <summary>وصف الباقة بالعربية في هذه النسخة.</summary>
    [MaxLength(2000)]
    public string DescriptionAr { get; set; } = "";

    /// <summary>وصف الباقة بالإنجليزية في هذه النسخة.</summary>
    [MaxLength(2000)]
    public string DescriptionEn { get; set; } = "";

    /// <summary>نص شارة الباقة بالعربية.</summary>
    [MaxLength(80)]
    public string BadgeAr { get; set; } = "";

    /// <summary>نص شارة الباقة بالإنجليزية.</summary>
    [MaxLength(80)]
    public string BadgeEn { get; set; } = "";

    /// <summary>سعر هذه النسخة من الباقة بالشيكل.</summary>
    [Precision(18, 2)]
    public decimal PriceIls { get; set; }
    /// <summary>مدة الاشتراك بالأيام.</summary>
    public int Days { get; set; } = 30;
    /// <summary>قناع أنواع الفرص التي تسمح الباقة بعرض تفاصيلها.</summary>
    public int DetailKinds { get; set; }
    /// <summary>قناع بتات أنواع الفرص التي يشملها إرسال التنبيهات.</summary>
    public int AlertKinds { get; set; }
    /// <summary>أقصى عدد للمجالات المفضلة؛ القيمة الفارغة تسمح بعدد غير محدود.</summary>
    [Range(1, 1000)]
    public int? MaxPreferredCategories { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Name(string language) => language == "en" ? NameEn : NameAr;
}
