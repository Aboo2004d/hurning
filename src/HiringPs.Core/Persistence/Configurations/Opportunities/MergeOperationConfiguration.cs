using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MergeOperation مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MergeOperationConfiguration : IEntityTypeConfiguration<MergeOperation>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
