using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين AiUsage مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
