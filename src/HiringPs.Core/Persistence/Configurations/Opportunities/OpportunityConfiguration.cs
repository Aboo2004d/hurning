using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين Opportunity مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder
            .HasIndex(x => new
            {
                x.Status,
                x.Kind,
                x.Deadline,
            });
        builder
            .Property(x => x.SortDate)
            .HasComputedColumnSql("COALESCE([SourcePublishedAt], [CreatedAt])", stored: true);
        builder.HasIndex(x => new { x.Status, x.Kind, x.SortDate });
        builder.HasIndex(x => x.Fingerprint);
        builder.HasIndex(x => new { x.Kind, x.NormalizedOrganization });
        builder
            .HasIndex(x => new
            {
                x.Kind,
                x.NormalizedOrganization,
                x.NormalizedReference,
            });
    }
}
