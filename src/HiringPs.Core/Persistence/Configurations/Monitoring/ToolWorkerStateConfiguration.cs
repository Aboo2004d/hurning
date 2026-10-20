using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="ToolWorkerState"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class ToolWorkerStateConfiguration : IEntityTypeConfiguration<ToolWorkerState>
{
    /// <summary>يطبق إعدادات تخزين <see cref="ToolWorkerState"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<ToolWorkerState> builder)
    {
        builder.HasIndex(x => x.At);
    }
}
