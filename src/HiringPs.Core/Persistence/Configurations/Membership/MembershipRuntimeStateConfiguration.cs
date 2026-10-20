using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="MembershipRuntimeState"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class MembershipRuntimeStateConfiguration : IEntityTypeConfiguration<MembershipRuntimeState>
{
    /// <summary>يطبق إعدادات تخزين <see cref="MembershipRuntimeState"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<MembershipRuntimeState> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasData(new MembershipRuntimeState { Id = 1 });
    }
}
