using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين OpportunitySourceLink مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class OpportunitySourceLinkConfiguration : IEntityTypeConfiguration<OpportunitySourceLink>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
