using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>يعرّف علاقات وفهارس وتخزين MembershipRuntimeState مع الحفاظ على مخطط تحديث المستخدم.</summary>
public sealed class MembershipRuntimeStateConfiguration : IEntityTypeConfiguration<MembershipRuntimeState>
{
    /// <summary>يطبق تعريف جدول الكيان؛ لا ينفذ migration أو اتصال قاعدة بيانات.</summary>
    public void Configure(EntityTypeBuilder<MembershipRuntimeState> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasData(new MembershipRuntimeState { Id = 1 });
    }
}
