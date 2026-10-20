using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

// IndexNow verifies ownership by fetching /{key}.txt at the site root (outside language prefixes).
[AllowAnonymous]
public sealed class IndexNowKeyController(IConfiguration configuration) : Controller
{
    [HttpGet("/{key}.txt")]
    public IActionResult Key(string key)
    {
        var configured = IndexNowSettings.Key(configuration);
        if (configured is null || !string.Equals(key, configured, StringComparison.Ordinal))
            return NotFound();
        return Content(configured, "text/plain; charset=utf-8");
    }
}
