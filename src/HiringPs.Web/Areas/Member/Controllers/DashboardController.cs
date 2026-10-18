using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class DashboardController(HiringDb db, TelegramClient telegram) : MemberControllerBase(db)
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(
            new MemberDashboard
            {
                Member = Profile,
                Subscriptions = await Db
                    .MemberSubscriptions.AsNoTracking()
                    .Include(x => x.PlanVersion)
                    .Where(x =>
                        x.UserId == MemberId && !x.Cancelled && x.EndsAt > DateTimeOffset.UtcNow
                    )
                    .OrderBy(x => x.StartsAt)
                    .ToListAsync(ct),
                SavedCount = await Db.SavedOpportunities.CountAsync(x => x.UserId == MemberId, ct),
                BotReady = telegram.Ready,
                DeliveryError =
                    await Db
                        .MemberMessages.Where(x =>
                            x.UserId == MemberId
                            && x.SentAt == null
                            && x.CancelledAt == null
                            && x.LastError != ""
                        )
                        .OrderByDescending(x => x.Id)
                        .Select(x => x.LastError)
                        .FirstOrDefaultAsync(ct)
                    ?? "",
            }
        );
}
