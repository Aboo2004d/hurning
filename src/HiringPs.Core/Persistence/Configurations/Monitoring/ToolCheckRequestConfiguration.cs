using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="ToolCheckRequest"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class ToolCheckRequestConfiguration : IEntityTypeConfiguration<ToolCheckRequest>
{
    /// <summary>يطبق إعدادات تخزين <see cref="ToolCheckRequest"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<ToolCheckRequest> builder)
    {
        builder
            .HasIndex(x => x.ActiveSlot)
            .IsUnique()
            .HasFilter("[ActiveSlot] IS NOT NULL");
        builder.HasIndex(x => new { x.Kind, x.RequestedAt });
    }
}
