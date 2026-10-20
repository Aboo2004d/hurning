using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="MergeOperation"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class MergeOperationConfiguration : IEntityTypeConfiguration<MergeOperation>
{
    /// <summary>يطبق إعدادات تخزين <see cref="MergeOperation"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<MergeOperation> builder)
    {
        builder
            .HasIndex(x => new
            {
                x.DuplicateId,
                x.TargetId,
                x.UndoneAt,
            });
    }
}
