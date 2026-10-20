using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MemberPreference مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MemberPreferenceConfiguration : IEntityTypeConfiguration<MemberPreference>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<MemberPreference> builder)
    {
        builder
            .HasOne<MemberProfile>()
            .WithOne()
            .HasForeignKey<MemberPreference>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
