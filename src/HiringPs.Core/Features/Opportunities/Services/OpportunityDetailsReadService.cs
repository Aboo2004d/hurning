using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يقرأ تفاصيل قابلة للعرض؛ يظل فحص صلاحية المشاهد مطلوبًا قبل تسليمها إلى الواجهة.</summary>
public sealed class OpportunityDetailsReadService(HiringDb db)
{
    /// <summary>يقرأ المنشور أو المؤرشف فقط مع أسماء وروابط مصادره دون محتوى المصدر أو إعداداته.</summary>
    public async Task<OpportunityDetailsDto?> ReadAsync(long id, CancellationToken ct)
    {
        var snapshot = await db.Opportunities.AsNoTracking()
            .Where(x => x.Id == id && (x.Status == PublicationStatus.Published || x.Status == PublicationStatus.Archived))
            .Select(x => new
            {
                Item = new OpportunityDetailsDto
                {
                    Id = x.Id,
                    Kind = x.Kind,
                    Title = x.Title,
                    Organization = x.Organization,
                    OriginalLanguage = x.OriginalLanguage,
                    Summary = x.Summary,
                    Locations = x.Locations,
                    Category = x.Category,
                    EmploymentType = x.EmploymentType,
                    ReferenceNumber = x.ReferenceNumber,
                    Deadline = x.Deadline,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    SourcePublishedAt = x.SourcePublishedAt,
                    SourceModifiedAt = x.SourceModifiedAt,
                    ExperienceLevel = x.ExperienceLevel,
                    Duration = x.Duration,
                    Cost = x.Cost,
                    Eligibility = x.Eligibility,
                    ApplyUrl = x.ApplyUrl,
                    LastVerifiedAt = x.LastVerifiedAt,
                    NormalizedOrganization = x.NormalizedOrganization,
                    LocationSearchText = x.LocationSearchText,
                    Sources = x.SourceLinks.Select(link => new OpportunitySourceSummaryDto(
                        link.SourceItem.Source.Name, link.SourceItem.Url)).ToList(),
                },
                x.AttachmentsJson,
            }).FirstOrDefaultAsync(ct);
        return snapshot is null ? null : snapshot.Item with
        {
            Attachments = PublicAttachmentLinks.Read(snapshot.AttachmentsJson),
        };
    }

    /// <summary>يسقط الاقتباسات المثبتة بترتيب التسجيل السابق، دون تسليم كيانات أدلة قابلة للتتبع.</summary>
    public Task<List<VerifiedOpportunityDateDto>> DatesAsync(long id, CancellationToken ct) =>
        db.OpportunityDates.AsNoTracking().Where(x => x.OpportunityId == id)
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id)
            .Select(x => new VerifiedOpportunityDateDto(x.Kind, x.Date, x.Label, x.Evidence, x.IsExtension))
            .ToListAsync(ct);
}
