using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MemberChallenge مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MemberChallengeConfiguration : IEntityTypeConfiguration<MemberChallenge>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<MemberChallenge> builder)
    {
        builder.HasIndex(x => x.LinkHash).IsUnique();
        builder
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
