using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين ToolCheckRequest مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class ToolCheckRequestConfiguration : IEntityTypeConfiguration<ToolCheckRequest>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<ToolCheckRequest> builder)
    {
        builder
            .HasIndex(x => x.ActiveSlot)
            .IsUnique()
            .HasFilter("[ActiveSlot] IS NOT NULL");
        builder.HasIndex(x => new { x.Kind, x.RequestedAt });
    }
}
