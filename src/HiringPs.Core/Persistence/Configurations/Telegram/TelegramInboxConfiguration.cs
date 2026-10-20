using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين TelegramInbox مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class TelegramInboxConfiguration : IEntityTypeConfiguration<TelegramInbox>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<TelegramInbox> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ProcessedAt, x.LockedUntil });
    }
}
