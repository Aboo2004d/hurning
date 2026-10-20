using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين WalletPaymentSession مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class WalletPaymentSessionConfiguration : IEntityTypeConfiguration<WalletPaymentSession>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<WalletPaymentSession> builder)
    {
        builder
            .Property(x => x.GatewayEnvironment)
            .HasDefaultValue("Production");
        builder
            .HasOne(x => x.Order)
            .WithOne()
            .HasForeignKey<WalletPaymentSession>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(x => new { x.GatewayEnvironment, x.TransactionId })
            .IsUnique()
            .HasFilter("[TransactionId] IS NOT NULL");
        builder.HasIndex(x => x.CreatedAt);
        builder
            .HasIndex(x => new { x.GatewayEnvironment, x.SessionKeyHash })
            .IsUnique()
            .HasFilter("[SessionKeyHash] IS NOT NULL");
    }
}
