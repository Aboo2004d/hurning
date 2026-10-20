using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="MembershipPlan"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    /// <summary>يطبق إعدادات تخزين <see cref="MembershipPlan"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder
            .HasOne<MembershipPlanVersion>()
            .WithMany()
            .HasForeignKey(x => x.CurrentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
