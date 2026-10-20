using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="OpportunityDateRecord"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class OpportunityDateRecordConfiguration : IEntityTypeConfiguration<OpportunityDateRecord>
{
    /// <summary>يطبق إعدادات تخزين <see cref="OpportunityDateRecord"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<OpportunityDateRecord> builder)
    {
        builder
            .HasOne(x => x.Opportunity)
            .WithMany()
            .HasForeignKey(x => x.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(x => new
            {
                x.OpportunityId,
                x.Kind,
                x.EvidenceHash,
            })
            .IsUnique();
    }
}
