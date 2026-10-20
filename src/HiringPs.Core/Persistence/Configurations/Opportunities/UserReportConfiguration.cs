using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين UserReport مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder
            .HasOne(x => x.Opportunity)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
