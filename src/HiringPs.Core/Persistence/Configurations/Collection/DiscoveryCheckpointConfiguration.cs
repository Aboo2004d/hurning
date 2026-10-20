using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين DiscoveryCheckpoint مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class DiscoveryCheckpointConfiguration : IEntityTypeConfiguration<DiscoveryCheckpoint>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<DiscoveryCheckpoint> builder)
    {
        builder.Property(x => x.SourceId).ValueGeneratedNever();
    }
}
