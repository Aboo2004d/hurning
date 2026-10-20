using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MemberProfile مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MemberProfileConfiguration : IEntityTypeConfiguration<MemberProfile>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
