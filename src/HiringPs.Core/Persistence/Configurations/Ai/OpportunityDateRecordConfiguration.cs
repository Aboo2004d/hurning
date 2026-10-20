using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين OpportunityDateRecord مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class OpportunityDateRecordConfiguration : IEntityTypeConfiguration<OpportunityDateRecord>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
