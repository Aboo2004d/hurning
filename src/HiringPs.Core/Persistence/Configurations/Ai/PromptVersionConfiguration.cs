using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين PromptVersion مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.HasData(PromptDefaults.Initial());
    }
}
