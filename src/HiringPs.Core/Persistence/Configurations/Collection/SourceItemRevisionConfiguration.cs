using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين SourceItemRevision مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class SourceItemRevisionConfiguration : IEntityTypeConfiguration<SourceItemRevision>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<SourceItemRevision> builder)
    {
        builder.HasIndex(x => new { x.SourceItemId, x.CapturedAt });
        builder.HasIndex(x => x.CapturedAt);
    }
}
