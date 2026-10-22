using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>يعرض العضو والبريد والباقة للتأكيد قبل تنفيذ المنح.</summary>
public sealed record SubscriptionGrantConfirmation(SubscriptionGrantInput Input, AdminMemberIdentityDto Member,
    MembershipPlanLabelDto Plan);
