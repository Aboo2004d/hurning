using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين WorkflowSettings مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class WorkflowSettingsConfiguration : IEntityTypeConfiguration<WorkflowSettings>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
