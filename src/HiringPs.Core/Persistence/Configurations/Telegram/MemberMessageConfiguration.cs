using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="MemberMessage"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class MemberMessageConfiguration : IEntityTypeConfiguration<MemberMessage>
{
    /// <summary>يطبق إعدادات تخزين <see cref="MemberMessage"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<MemberMessage> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.EventKey }).IsUnique();
        builder
            .HasIndex(x => new
            {
                x.SentAt,
                x.CancelledAt,
                x.AvailableAt,
            });
        builder
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
