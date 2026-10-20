using System.Linq.Expressions;

namespace HiringPs.Core;

/// <summary>إسقاطات SQL الصريحة للبطاقات والتفاصيل؛ تبقي أعمدة المراجعة والتزامن خارج نماذج الزائر.</summary>
public static class PublicOpportunityProjection
{
    /// <summary>الحقول اللازمة للبطاقة وRSS فقط، دون تحميل علاقات أو محتوى مصدر.</summary>
    public static readonly Expression<Func<Opportunity, OpportunityCardDto>> Card = x => new OpportunityCardDto
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
    };
}
