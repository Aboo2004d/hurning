using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين DuplicateCandidate مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class DuplicateCandidateConfiguration : IEntityTypeConfiguration<DuplicateCandidate>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<DuplicateCandidate> builder)
    {
        builder
            .HasIndex(x => new { x.OpportunityId, x.CandidateId })
            .IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder
            .HasOne(x => x.Opportunity)
            .WithMany()
            .HasForeignKey(x => x.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(x => x.Candidate)
            .WithMany()
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
