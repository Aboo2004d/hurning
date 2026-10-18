using System.Security.Claims;
using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class LanguageController(HiringDb db) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Switch(string culture, string? returnUrl)
    {
        if (!LanguageUrls.IsSupported(culture))
            return BadRequest();
        LanguageUrls.Save(HttpContext, culture);
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var member = id is null
            ? null
            : await db.MemberProfiles.SingleOrDefaultAsync(
                x => x.UserId == id,
                HttpContext.RequestAborted
            );
        if (member is not null)
        {
            member.Language = culture;
            await db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        var destination = Url.IsLocalUrl(returnUrl)
            ? Url.Content(returnUrl!)
            : Request.PathBase + "/";
        var mount = HttpContext.Items["MountPath"] is PathString path ? path : PathString.Empty;
        return LocalRedirect(LanguageUrls.Switch(destination, mount, culture));
    }
}
