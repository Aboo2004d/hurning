using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MemberMessage مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MemberMessageConfiguration : IEntityTypeConfiguration<MemberMessage>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
