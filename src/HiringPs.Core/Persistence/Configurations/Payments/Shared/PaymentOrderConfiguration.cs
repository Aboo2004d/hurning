using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="PaymentOrder"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    /// <summary>يطبق إعدادات تخزين <see cref="PaymentOrder"/> عند إنشاء نموذج قاعدة البيانات.</summary>
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
