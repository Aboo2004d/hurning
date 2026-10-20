using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>كتالوج باقات وفترات العضو للعرض دون كيانات قابلة للتتبع.</summary>
public sealed class PlanCatalog
{
    public List<MembershipPlanVersionSummaryDto> Plans { get; set; } = [];
    public List<MemberSubscriptionPeriodDto> Subscriptions { get; set; } = [];
}
