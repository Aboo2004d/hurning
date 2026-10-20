using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="RetentionPolicy"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class RetentionPolicyConfiguration : IEntityTypeConfiguration<RetentionPolicy>
{
    /// <summary>يطبق إعدادات تخزين <see cref="RetentionPolicy"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasData(new RetentionPolicy { Id = 1 });
    }
}
