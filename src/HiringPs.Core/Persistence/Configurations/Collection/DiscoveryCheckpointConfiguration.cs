using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="DiscoveryCheckpoint"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class DiscoveryCheckpointConfiguration : IEntityTypeConfiguration<DiscoveryCheckpoint>
{
    /// <summary>يطبق إعدادات تخزين <see cref="DiscoveryCheckpoint"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<DiscoveryCheckpoint> builder)
    {
        builder.Property(x => x.SourceId).ValueGeneratedNever();
    }
}
