namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـOpportunityDateKind؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum OpportunityDateKind
{
    Publication = 0,
    Modification = 1,
    Deadline = 2,
    BookletPurchase = 3,
    SiteVisit = 4,
    BidOpening = 5,
    Other = 6,
}
