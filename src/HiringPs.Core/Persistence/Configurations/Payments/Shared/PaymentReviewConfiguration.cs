using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="PaymentReview"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class PaymentReviewConfiguration : IEntityTypeConfiguration<PaymentReview>
{
    /// <summary>يطبق إعدادات تخزين <see cref="PaymentReview"/> عند إنشاء نموذج قاعدة البيانات.</summary>
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
