using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="OpportunitySourceLink"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class OpportunitySourceLinkConfiguration : IEntityTypeConfiguration<OpportunitySourceLink>
{
    /// <summary>يطبق إعدادات تخزين <see cref="OpportunitySourceLink"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<OpportunitySourceLink> builder)
    {
        builder
            .HasIndex(x => new { x.SourceItemId, x.ChildKey })
            .IsUnique();
        builder
            .HasOne(x => x.SourceItem)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
