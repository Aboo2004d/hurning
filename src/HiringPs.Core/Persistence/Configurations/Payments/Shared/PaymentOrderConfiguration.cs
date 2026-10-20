using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين PaymentOrder مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.Property(x => x.GatewayEnvironment).HasDefaultValue("Production");
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
            .HasOne<DiscountCode>()
            .WithMany()
            .HasForeignKey(x => x.DiscountCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(x => new
            {
                x.Gateway,
                x.GatewayEnvironment,
                x.ProviderReference,
            })
            .IsUnique()
            .HasFilter("[ProviderReference] IS NOT NULL");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
