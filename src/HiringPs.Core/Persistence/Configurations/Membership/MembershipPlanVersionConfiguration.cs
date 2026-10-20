using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MembershipPlanVersion مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MembershipPlanVersionConfiguration : IEntityTypeConfiguration<MembershipPlanVersion>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<MembershipPlanVersion> builder)
    {
        builder
            .HasOne<MembershipPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
