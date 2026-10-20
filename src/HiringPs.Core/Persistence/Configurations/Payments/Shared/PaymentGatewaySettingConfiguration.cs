using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين PaymentGatewaySetting مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class PaymentGatewaySettingConfiguration : IEntityTypeConfiguration<PaymentGatewaySetting>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<PaymentGatewaySetting> builder)
    {
        builder
            .HasData(
                new PaymentGatewaySetting { Key = "Lahza" },
                new PaymentGatewaySetting { Key = "JawwalPay" }
            );
    }
}
