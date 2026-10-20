using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين ToolCheckResult مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class ToolCheckResultConfiguration : IEntityTypeConfiguration<ToolCheckResult>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<ToolCheckResult> builder)
    {
        builder.HasIndex(x => new { x.Tool, x.CheckedAt });
        builder
            .HasOne<ToolCheckRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
