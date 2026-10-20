using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="OpportunityAccessRule"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class OpportunityAccessRuleConfiguration : IEntityTypeConfiguration<OpportunityAccessRule>
{
    /// <summary>يطبق إعدادات تخزين <see cref="OpportunityAccessRule"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<OpportunityAccessRule> builder)
    {
        builder.Property(x => x.Kind).ValueGeneratedNever();
        builder
            .HasData(
                Enum.GetValues<OpportunityKind>()
                    .Select(x => new OpportunityAccessRule { Kind = x, PublicDetails = true })
            );
    }
}
