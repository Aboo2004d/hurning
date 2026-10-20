using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>فهرس ترتيب سجل العمليات بالتاريخ والمعرف، دون تغيير حقول التدقيق أو بياناتها.</summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <summary>يسرع قراءة أحدث الأحداث والترقيم والترشيح الزمني.</summary>
    public void Configure(EntityTypeBuilder<AuditEntry> builder) => builder.HasIndex(x => new { x.At, x.Id });
}
