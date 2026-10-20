using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين ContactMessage مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder
            .HasIndex(x => new
            {
                x.IsHandled,
                x.IsRead,
                x.CreatedAt,
            });
    }
}
