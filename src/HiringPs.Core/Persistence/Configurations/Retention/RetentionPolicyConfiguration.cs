using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين RetentionPolicy مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class RetentionPolicyConfiguration : IEntityTypeConfiguration<RetentionPolicy>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasData(new RetentionPolicy { Id = 1 });
    }
}
