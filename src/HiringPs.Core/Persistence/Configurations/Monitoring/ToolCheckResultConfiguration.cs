using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="ToolCheckResult"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class ToolCheckResultConfiguration : IEntityTypeConfiguration<ToolCheckResult>
{
    /// <summary>يطبق إعدادات تخزين <see cref="ToolCheckResult"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<ToolCheckResult> builder)
    {
        builder.HasIndex(x => new { x.Tool, x.CheckedAt });
        builder
            .HasOne<ToolCheckRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
