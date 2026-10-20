using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين ToolWorkerState مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class ToolWorkerStateConfiguration : IEntityTypeConfiguration<ToolWorkerState>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<ToolWorkerState> builder)
    {
        builder.HasIndex(x => x.At);
    }
}
