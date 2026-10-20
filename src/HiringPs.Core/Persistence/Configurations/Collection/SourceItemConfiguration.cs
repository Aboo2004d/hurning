using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين SourceItem مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class SourceItemConfiguration : IEntityTypeConfiguration<SourceItem>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<SourceItem> builder)
    {
        builder.HasIndex(x => new { x.SourceId, x.ExternalId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.RetryAt });
        builder.HasIndex(x => x.RequestedPromptVersionId);
    }
}
