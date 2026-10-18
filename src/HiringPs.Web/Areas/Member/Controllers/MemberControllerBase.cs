using System.Security.Claims;
using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

[Area("Member"), Authorize]
public abstract class MemberControllerBase(HiringDb db) : Controller
{
    protected HiringDb Db { get; } = db;
    protected string MemberId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    protected MemberProfile Profile { get; private set; } = null!;

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var member = await Db.MemberProfiles.SingleOrDefaultAsync(
            x => x.UserId == MemberId,
            HttpContext.RequestAborted
        );
        if (member is null || !member.Activated)
        {
            context.Result = Forbid();
            return;
        }
        if (member.Suspended)
        {
            Response.StatusCode = 403;
            context.Result = View("/Views/Account/Suspended.cshtml", member.SuspensionReason);
            return;
        }
        Profile = member;
        await next();
    }
}
