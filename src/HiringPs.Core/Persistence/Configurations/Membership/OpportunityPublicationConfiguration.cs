using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين OpportunityPublication مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class OpportunityPublicationConfiguration : IEntityTypeConfiguration<OpportunityPublication>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
