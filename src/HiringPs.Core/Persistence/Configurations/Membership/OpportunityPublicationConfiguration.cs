using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="OpportunityPublication"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class OpportunityPublicationConfiguration : IEntityTypeConfiguration<OpportunityPublication>
{
    /// <summary>يطبق إعدادات تخزين <see cref="OpportunityPublication"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<OpportunityPublication> builder)
    {
        builder.Property(x => x.OpportunityId).ValueGeneratedNever();
        builder.Property(x => x.AfterUserId).HasMaxLength(450);
        builder
            .HasOne(x => x.Opportunity)
            .WithOne()
            .HasForeignKey<OpportunityPublication>(x => x.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
