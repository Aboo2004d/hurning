using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="WalletPaymentSession"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class WalletPaymentSessionConfiguration : IEntityTypeConfiguration<WalletPaymentSession>
{
    /// <summary>يطبق إعدادات تخزين <see cref="WalletPaymentSession"/> عند إنشاء نموذج قاعدة البيانات.</summary>
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
