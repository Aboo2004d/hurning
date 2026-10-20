using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="Source"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class SourceEntityConfiguration : IEntityTypeConfiguration<Source>
{
    /// <summary>يطبق إعدادات تخزين <see cref="Source"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder
            .ToTable(t =>
                t.HasCheckConstraint("CK_Source_Interval", "[IntervalHours] BETWEEN 1 AND 168")
            );
    }
}
