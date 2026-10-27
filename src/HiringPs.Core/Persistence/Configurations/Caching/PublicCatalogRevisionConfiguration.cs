using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HiringPs.Core;

/// <summary>تعريف صف إصدار كاش الكتالوج المشترك بين عمليتي الجمع والويب.</summary>
public sealed class PublicCatalogRevisionConfiguration : IEntityTypeConfiguration<PublicCatalogRevision>
{
    public void Configure(EntityTypeBuilder<PublicCatalogRevision> builder)
    {
        builder.ToTable("PublicCatalogRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasData(new PublicCatalogRevision { Id = 1, Version = 0 });
    }
}
