using System.ComponentModel.DataAnnotations;

namespace HiringPs.Web.Models;

/// <summary>حقول نموذج إدارة الخصم دون ربط كيان قاعدة البيانات بطلب HTTP.</summary>
public sealed class DiscountInput
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Code { get; set; } = "";
    public int? PlanId { get; set; }
    [Range(0, 100)] public int Percent { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    [Range(1, int.MaxValue)] public int MaximumUses { get; set; }
    public bool Enabled { get; set; } = true;
}
