using System.Diagnostics;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

[AllowAnonymous, IgnoreAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ErrorsController(ILogger<ErrorsController> logger) : Controller
{
    public IActionResult Status(int? code = null)
    {
        code ??= Response.StatusCode >= 400 ? Response.StatusCode : 500;
        if (code is not (400 or 401 or 403 or 404 or 408 or 429 or 500 or 502 or 503 or 504))
            code = 500;
        Response.StatusCode = code.Value;
        if (code == 400)
        {
            var originalPath =
                HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
                ?? Request.Path.Value
                ?? "";
            var endpoint =
                originalPath.EndsWith("/Google/Complete", StringComparison.OrdinalIgnoreCase)
                    ? "Google.Complete"
                : originalPath.EndsWith("/Registration/Verify", StringComparison.OrdinalIgnoreCase)
                    ? "Registration.Verify"
                : "OtherEndpoint";
            logger.LogWarning(
                "RequestRejected Status 400 Endpoint {Endpoint} TraceId {TraceId} GoogleAntiforgery {GoogleAntiforgery}",
                endpoint,
                Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                HttpContext.Items[GoogleCompletionDiagnostics.RejectedItem] is true
            );
        }
        return View(
            "/Views/Shared/Error.cshtml",
            new ErrorViewModel
            {
                StatusCode = code.Value,
                GoogleFormExpired =
                    code == 400
                    && HttpContext.Items[GoogleCompletionDiagnostics.RejectedItem] is true,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            }
        );
    }
}
