namespace HiringPs.Core;

/// <summary>بيانات تجهيز منشور جاهزة للعرض مع سجل مختصر ووجهات خالية من الرموز السرية.</summary>
public sealed record SocialComposeDataDto(long OpportunityId, string Provider,
    SocialOpportunitySummaryDto Opportunity, string Text, long TemplateVersionId,
    int TemplateNumber, string Link, List<SocialPublicationSummaryDto> History,
    List<SocialDestinationSummaryDto> Destinations, bool PreviouslyPublished);
