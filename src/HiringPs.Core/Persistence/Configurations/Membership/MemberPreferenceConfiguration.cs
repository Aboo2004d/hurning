using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>
/// يعرّف العلاقات والفهارس والقيود والبيانات الابتدائية لكيان <see cref="MemberPreference"/>.
/// تبقى أسماء الجداول والحقول وقواعد الحذف مطابقة للتعريف السابق.
/// </summary>
public sealed class MemberPreferenceConfiguration : IEntityTypeConfiguration<MemberPreference>
{
    /// <summary>يطبق إعدادات تخزين <see cref="MemberPreference"/> عند إنشاء نموذج قاعدة البيانات.</summary>
    public void Configure(EntityTypeBuilder<MemberPreference> builder)
    {
        builder
            .HasOne<MemberProfile>()
            .WithOne()
            .HasForeignKey<MemberPreference>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
