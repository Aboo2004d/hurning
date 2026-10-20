using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="Opportunity"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    /// <summary>يطبق إعدادات تخزين <see cref="Opportunity"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder
            .HasIndex(x => new
            {
                x.Status,
                x.Kind,
                x.Deadline,
            });
        builder.HasIndex(x => x.Fingerprint);
        builder.HasIndex(x => new { x.Kind, x.NormalizedOrganization });
    }
}
