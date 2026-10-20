using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="AiUsage"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    /// <summary>يطبق إعدادات تخزين <see cref="AiUsage"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<AiUsage> builder)
    {
        builder
            .HasOne<PromptVersion>()
            .WithMany()
            .HasForeignKey(x => x.PromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(x => new
            {
                x.StartedAt,
                x.PromptVersionId,
                x.Model,
            });
    }
}
