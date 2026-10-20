using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="SocialTemplateVersion"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class SocialTemplateVersionConfiguration : IEntityTypeConfiguration<SocialTemplateVersion>
{
    /// <summary>يطبق إعدادات تخزين <see cref="SocialTemplateVersion"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<SocialTemplateVersion> builder)
    {
        builder.HasIndex(x => new { x.Provider, x.Scope, x.Number }).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.Scope }).IsUnique().HasFilter("[Active] = 1");
    }
}
