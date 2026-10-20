using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HiringPs.Web.Models;

/// <summary>حقول مراجعة الشراء مع بيانات عرض يعيدها الخادم؛ شروط السعر المعروضة تتحقق قبل حجز الطلب.</summary>
public sealed class PurchaseInput
{
    public int PlanId { get; set; }
    public int PresentedVersionId { get; set; }
    public decimal PresentedAmount { get; set; }
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [StringLength(50)]
    public string DiscountCode { get; set; } = "";
    public string Gateway { get; set; } = "";
    public bool AcceptTerms { get; set; }
    [BindNever]
    public MembershipPlanVersionSummaryDto? Plan { get; set; }
    [BindNever]
    public string[] Gateways { get; set; } = [];
    [BindNever]
    public decimal FinalAmount { get; set; }
    [BindNever]
    public DateTimeOffset StartsAt { get; set; }
}
