using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="MemberProfile"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class MemberProfileConfiguration : IEntityTypeConfiguration<MemberProfile>
{
    /// <summary>يطبق إعدادات تخزين <see cref="MemberProfile"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<MemberProfile> builder)
    {
        builder
            .HasOne<IdentityUser>()
            .WithOne()
            .HasForeignKey<MemberProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(x => x.TelegramUserId)
            .IsUnique()
            .HasFilter("[TelegramUserId] IS NOT NULL");
        builder.HasIndex(x => x.Mobile);
    }
}
