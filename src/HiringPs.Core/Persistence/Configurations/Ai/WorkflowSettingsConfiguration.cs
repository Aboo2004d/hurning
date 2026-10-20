using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="WorkflowSettings"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class WorkflowSettingsConfiguration : IEntityTypeConfiguration<WorkflowSettings>
{
    /// <summary>يطبق إعدادات تخزين <see cref="WorkflowSettings"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<WorkflowSettings> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder
            .HasData(new WorkflowSettings { Id = 1, ActivePromptVersionId = 1 });
        builder
            .HasOne<PromptVersion>()
            .WithMany()
            .HasForeignKey(x => x.ActivePromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<PromptVersion>()
            .WithMany()
            .HasForeignKey(x => x.PendingPromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
