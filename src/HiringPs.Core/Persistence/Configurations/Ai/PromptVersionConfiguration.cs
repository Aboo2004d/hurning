using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="PromptVersion"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    /// <summary>يطبق إعدادات تخزين <see cref="PromptVersion"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.HasData(PromptDefaults.Initial());
    }
}
