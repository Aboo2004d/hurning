using System.Diagnostics;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

[AllowAnonymous, IgnoreAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ErrorsController : Controller
{
    public IActionResult Status(int? code = null)
    {
        code ??= Response.StatusCode >= 400 ? Response.StatusCode : 500;
        if (code is not (400 or 401 or 403 or 404 or 408 or 429 or 500 or 502 or 503 or 504))
            code = 500;
        Response.StatusCode = code.Value;
        return View(
            "/Views/Shared/Error.cshtml",
            new ErrorViewModel
            {
                StatusCode = code.Value,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            }
        );
    }
}
