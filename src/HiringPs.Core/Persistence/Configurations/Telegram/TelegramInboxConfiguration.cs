using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="TelegramInbox"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class TelegramInboxConfiguration : IEntityTypeConfiguration<TelegramInbox>
{
    /// <summary>يطبق إعدادات تخزين <see cref="TelegramInbox"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<TelegramInbox> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ProcessedAt, x.LockedUntil });
    }
}
