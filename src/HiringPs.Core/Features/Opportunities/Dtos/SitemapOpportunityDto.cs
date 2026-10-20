namespace HiringPs.Core;

/// <summary>حقول عنوان رابط في خريطة الموقع وتاريخ تغير المحتوى، دون تاريخ التحقق.</summary>
public sealed record SitemapOpportunityDto(long Id, string Title, DateTimeOffset Changed);
