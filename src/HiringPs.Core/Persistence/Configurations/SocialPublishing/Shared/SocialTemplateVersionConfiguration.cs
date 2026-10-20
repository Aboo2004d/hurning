using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين SocialTemplateVersion مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class SocialTemplateVersionConfiguration : IEntityTypeConfiguration<SocialTemplateVersion>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<SocialTemplateVersion> builder)
    {
        builder.HasIndex(x => new { x.Provider, x.Scope, x.Number }).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.Scope }).IsUnique().HasFilter("[Active] = 1");
    }
}
