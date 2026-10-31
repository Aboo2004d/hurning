using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HiringPs.Core;
/// <summary>قيود منع تكرار المواعيد والوظائف، حتى مع تعدد نسخ السيرفر.</summary>
public sealed class RandomPublicationRunConfiguration : IEntityTypeConfiguration<RandomPublicationRun>
{
    /// <summary>يحجز كل موعد ووظيفة لوجهة واحدة مرة واحدة مع حفظ علاقات التاريخ.</summary>
    public void Configure(EntityTypeBuilder<RandomPublicationRun> b)
    {
        b.ToTable("RandomPublicationRuns");
        b.HasIndex(x => new { x.Provider, x.Day, x.Slot }).IsUnique();
        b.HasIndex(x => new { x.Provider, x.OpportunityId }).IsUnique().HasFilter("[OpportunityId] IS NOT NULL");
        b.HasOne<Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CreatedAt);
    }
}
