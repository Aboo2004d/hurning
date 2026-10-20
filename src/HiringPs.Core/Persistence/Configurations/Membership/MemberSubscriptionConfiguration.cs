using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="MemberSubscription"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class MemberSubscriptionConfiguration : IEntityTypeConfiguration<MemberSubscription>
{
    /// <summary>يطبق إعدادات تخزين <see cref="MemberSubscription"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<MemberSubscription> builder)
    {
        builder
            .Property(x => x.GatewayEnvironment)
            .HasDefaultValue("Production");
        builder
            .HasOne(x => x.PlanVersion)
            .WithMany()
            .HasForeignKey(x => x.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("[OrderId] IS NOT NULL");
        builder.HasIndex(x => new { x.UserId, x.EndsAt });
        builder
            .HasOne<PaymentOrder>()
            .WithOne()
            .HasForeignKey<MemberSubscription>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
