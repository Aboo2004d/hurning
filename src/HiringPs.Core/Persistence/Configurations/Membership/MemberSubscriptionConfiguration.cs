using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MemberSubscription مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MemberSubscriptionConfiguration : IEntityTypeConfiguration<MemberSubscription>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
