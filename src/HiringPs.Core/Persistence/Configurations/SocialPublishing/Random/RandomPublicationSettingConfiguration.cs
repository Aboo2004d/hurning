using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HiringPs.Core;
/// <summary>يفرض حدود إعدادات النشر اليومي على القاعدة.</summary>
public sealed class RandomPublicationSettingConfiguration : IEntityTypeConfiguration<RandomPublicationSetting>
{
    /// <summary>يحدد حدود العدد والأيام والوجهات المعتمدة.</summary>
    public void Configure(EntityTypeBuilder<RandomPublicationSetting> b)
    {
        b.ToTable("RandomPublicationSettings", t =>
        {
            t.HasCheckConstraint("CK_RandomSetting_Count", "[DailyCount] BETWEEN 1 AND 24");
            t.HasCheckConstraint("CK_RandomSetting_Days", "[MaxRemainingDays] BETWEEN 0 AND 3650");
            t.HasCheckConstraint("CK_RandomSetting_Provider", "[Provider] IN ('Facebook','LinkedInPersonal','LinkedIn')");
        });
    }
}
