using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين DiscountCode مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
