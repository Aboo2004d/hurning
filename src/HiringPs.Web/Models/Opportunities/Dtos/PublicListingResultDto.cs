using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>نتيجة قراءة الرئيسية مع قوائم الفلاتر والقسم المستقل للوظائف القريبة من الإغلاق.</summary>
public sealed record PublicListingResultDto(
    OpportunityPageDto Page,
    FilterValues Filters,
    List<OpportunityCardDto> ClosingSoon
);
