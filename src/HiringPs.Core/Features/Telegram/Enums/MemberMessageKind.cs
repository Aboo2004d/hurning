namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـMemberMessageKind؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum MemberMessageKind
{
    Opportunity = 0,
    SavedDeadline = 1,
    Payment = 2,
    Subscription = 3,
    Verification = 4,
    Review = 5,
}
