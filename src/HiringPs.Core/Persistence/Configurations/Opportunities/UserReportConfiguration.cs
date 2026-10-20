using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="UserReport"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    /// <summary>يطبق إعدادات تخزين <see cref="UserReport"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder
            .HasOne(x => x.Opportunity)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
