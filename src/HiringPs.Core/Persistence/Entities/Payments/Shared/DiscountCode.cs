using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>كود خصم محدود الصلاحية والاستخدام، اختياري التقييد بباقة معينة.</summary>
public sealed class DiscountCode
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public int Id { get; set; }

    /// <summary>الكود المعتمد للخصم أو نتيجة الفحص بحسب نوع السجل.</summary>
    [MaxLength(50)]
    public string Code { get; set; } = "";

    // A null plan means this discount is available for every subscription plan.
    /// <summary>معرّف الباقة؛ القيمة الفارغة في الخصومات تعني أن الكود يشمل جميع الباقات.</summary>
    public int? PlanId { get; set; }

    /// <summary>نسبة الخصم من صفر إلى مئة.</summary>
    [Range(0, 100)]
    public int Percent { get; set; }
    /// <summary>وقت انتهاء صلاحية السجل أو التفويض؛ لا يقبل الاستخدام بعده.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>الحد الأقصى لعدد مرات استخدام كود الخصم.</summary>
    public int MaximumUses { get; set; }
    /// <summary>يسمح باستخدام السجل أو التكامل؛ تعطيله لا يحذف البيانات السابقة.</summary>
    public bool Enabled { get; set; } = true;
}
