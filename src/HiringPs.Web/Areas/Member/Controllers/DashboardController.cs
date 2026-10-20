using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Member.Controllers;

public class DashboardController(
    HiringDb db,
    TelegramClient telegram,
    MemberPlanReadService plans,
    SavedOpportunityService saved,
    MemberAccountReadService accounts
) : MemberControllerBase(db)
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new MemberDashboard
        {
            Member = Profile,
            Subscriptions = await plans.PeriodsAsync(MemberId, ct),
            SavedCount = await saved.CountAsync(MemberId, ct),
            BotReady = telegram.Ready,
            DeliveryError = await accounts.DeliveryErrorAsync(MemberId, ct),
        });
}
