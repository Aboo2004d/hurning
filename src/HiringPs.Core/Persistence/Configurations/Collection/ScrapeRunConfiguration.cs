using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين ScrapeRun مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class ScrapeRunConfiguration : IEntityTypeConfiguration<ScrapeRun>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<ScrapeRun> builder)
    {
        builder.HasIndex(x => new { x.SourceId, x.Status, x.StartedAt });
    }
}
