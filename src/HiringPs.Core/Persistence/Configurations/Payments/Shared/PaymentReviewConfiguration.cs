using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين PaymentReview مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class PaymentReviewConfiguration : IEntityTypeConfiguration<PaymentReview>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<PaymentReview> builder)
    {
        builder
            .HasOne<PaymentOrder>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OrderId).IsUnique();
    }
}
