using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class MemberDashboard
{
    public MemberAccountSummaryDto Member { get; set; } = null!;
    public List<MemberSubscriptionPeriodDto> Subscriptions { get; set; } = [];
    public int SavedCount
    {
        get; set;
    }
    public bool BotReady
    {
        get; set;
    }
    public string DeliveryError { get; set; } = "";
}
