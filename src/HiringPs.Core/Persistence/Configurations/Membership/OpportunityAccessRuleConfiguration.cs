using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين OpportunityAccessRule مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class OpportunityAccessRuleConfiguration : IEntityTypeConfiguration<OpportunityAccessRule>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
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
