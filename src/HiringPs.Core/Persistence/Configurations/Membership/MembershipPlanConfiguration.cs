using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MembershipPlan مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder
            .HasOne<MembershipPlanVersion>()
            .WithMany()
            .HasForeignKey(x => x.CurrentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
