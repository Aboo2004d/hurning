using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين SocialPublication مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class SocialPublicationConfiguration : IEntityTypeConfiguration<SocialPublication>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<SocialPublication> builder)
    {
        builder.HasOne<SocialTemplateVersion>().WithMany().HasForeignKey(x => x.TemplateVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OpportunityId, x.Provider, x.CreatedAt });
        builder.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OpportunityId, x.Provider, x.PageId }).IsUnique().HasFilter("[Provider] = N'Telegram'");
        builder.HasIndex(x => new { x.Provider, x.Status, x.NextAttemptAt });
    }
}
