using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="DiscountCode"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    /// <summary>يطبق إعدادات تخزين <see cref="DiscountCode"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<DiscountCode> builder)
    {
        builder.HasIndex(x => x.Code).IsUnique();
        builder
            .HasOne<MembershipPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
