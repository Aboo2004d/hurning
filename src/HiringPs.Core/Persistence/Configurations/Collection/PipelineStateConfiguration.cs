using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين PipelineState مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class PipelineStateConfiguration : IEntityTypeConfiguration<PipelineState>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<PipelineState> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasData(new PipelineState { Id = 1 });
    }
}
