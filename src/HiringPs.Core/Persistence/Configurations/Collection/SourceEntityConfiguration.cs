using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين Source مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class SourceEntityConfiguration : IEntityTypeConfiguration<Source>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder
            .ToTable(t =>
                t.HasCheckConstraint("CK_Source_Interval", "[IntervalHours] BETWEEN 1 AND 168")
            );
    }
}
