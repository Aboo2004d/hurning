using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين OrganizationAlias مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class OrganizationAliasConfiguration : IEntityTypeConfiguration<OrganizationAlias>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<OrganizationAlias> builder)
    {
        builder.HasIndex(x => x.Alias).IsUnique();
        builder.HasIndex(x => x.Canonical);
    }
}
