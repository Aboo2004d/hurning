using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="PaymentGatewaySetting"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class PaymentGatewaySettingConfiguration : IEntityTypeConfiguration<PaymentGatewaySetting>
{
    /// <summary>يطبق إعدادات تخزين <see cref="PaymentGatewaySetting"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<PaymentGatewaySetting> builder)
    {
        builder
            .HasData(
                new PaymentGatewaySetting { Key = "Lahza" },
                new PaymentGatewaySetting { Key = "JawwalPay" }
            );
    }
}
