using HiringPs.Web.Services.Membership;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

/// <summary>تيار إشعارات للأسعار فقط؛ لا ينفذ شراءً أو يتصل ببوابات الدفع.</summary>
[Authorize]
public sealed class PlanUpdatesController(PlanCatalogNotifications notifications) : Controller
{
    /// <summary>يرسل إشعار التعديل فورًا، ونبضة كل 15 ثانية للتعافي بين نسخ الويب وإعادة الاتصال.</summary>
    [HttpGet]
    public async Task Stream(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        var subscription = notifications.Subscribe();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Response.WriteAsync("event: catalog\ndata: refresh\n\n", ct);
                await Response.Body.FlushAsync(ct);
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                wait.CancelAfter(TimeSpan.FromSeconds(15));
                try { await subscription.Reader.ReadAsync(wait.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { notifications.Unsubscribe(subscription.Id); }
    }
}
