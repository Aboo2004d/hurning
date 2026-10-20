using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين IndexNowEntry مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class IndexNowEntryConfiguration : IEntityTypeConfiguration<IndexNowEntry>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<IndexNowEntry> builder)
    {
        builder.HasOne(x => x.Opportunity).WithOne().HasForeignKey<IndexNowEntry>(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProcessedAt, x.QueuedAt });
    }
}
