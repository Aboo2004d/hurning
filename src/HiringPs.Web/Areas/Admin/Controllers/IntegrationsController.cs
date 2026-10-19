using System.Security.Claims;
using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IntegrationsController(HiringDb db, SocialPublishing social,
    IAuthenticationSchemeProvider schemes, SignInManager<IdentityUser> signIn) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.GoogleTest = await db.ToolCheckResults.AsNoTracking().Where(x => x.Tool == "GoogleInteractive")
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        return View(await db.SocialConnections.AsNoTracking().ToListAsync());
    }
    [HttpGet]
    public async Task<IActionResult> TestGoogle(string? returnUrl = null)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!;
        if (await schemes.GetSchemeAsync("Google") is null) { TempData["Notice"] = "IntegrationNotConfigured"; return LocalRedirect(destination); }
        var properties = signIn.ConfigureExternalAuthenticationProperties("Google",
            Url.Action(nameof(GoogleTestComplete)), User.FindFirstValue(ClaimTypes.NameIdentifier));
        properties.Items["GoogleAdminTest"] = "true";
        properties.Items["GoogleTestReturnUrl"] = destination;
        SocialAuthentication.UseInfrastructureCallbackBase(Request);
        return Challenge(properties, "Google");
    }
    public async Task<IActionResult> GoogleTestComplete(CancellationToken ct)
    {
        var ticket = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        var isTest = ticket.Properties is not null && ticket.Properties.Items.TryGetValue("GoogleAdminTest", out var purpose) && purpose == "true";
        var info = isTest ? await signIn.GetExternalLoginInfoAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)) : null;
        var returnUrl = isTest && ticket.Properties!.Items.TryGetValue("GoogleTestReturnUrl", out var savedReturn)
            && Url.IsLocalUrl(savedReturn) ? savedReturn! : Url.Action(nameof(Index))!;
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        if (info?.LoginProvider != "Google") { TempData["Notice"] = "GoogleFailed"; return LocalRedirect(returnUrl); }
        var request = new ToolCheckRequest { Kind = ToolCheckKind.GoogleLogin, Status = ToolRequestStatus.Completed,
            Actor = User.Identity?.Name ?? "Admin", StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow, Outcome = "ChecksFinished" };
        db.ToolCheckRequests.Add(request);
        // Save the request first to obtain its generated key; no membership or login changes.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        db.ToolCheckResults.Add(new ToolCheckResult { Tool = "GoogleInteractive", State = ToolState.Ready,
            Code = "GoogleInteractiveReady", RequestId = request.Id });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        TempData["Notice"] = "GoogleInteractiveReady";
        return LocalRedirect(returnUrl);
    }
    [HttpPost]
    public async Task<IActionResult> GoogleEnabled(bool enabled, CancellationToken ct)
    {
        var control = await db.SocialConnections.FindAsync(["Google"], ct);
        if (control is null) { control = new SocialConnection { Provider = "Google", PageName = "Google" }; db.SocialConnections.Add(control); }
        control.Enabled = enabled;
        db.Audit.Add(new AuditEntry { Actor = User.Identity?.Name ?? "Admin", Action = "Google.Enabled", Detail = enabled.ToString() });
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Connect(string provider, string? pageId = null)
    {
        if (!SocialPublishing.IsProvider(provider) || (provider != "LinkedInPersonal" && !System.Text.RegularExpressions.Regex.IsMatch(pageId ?? "", "^[0-9]{1,40}$"))) return BadRequest();
        if (await schemes.GetSchemeAsync("Publish" + provider) is null) { TempData["Notice"] = "IntegrationNotConfigured"; return RedirectToAction(nameof(Index)); }
        var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(Connected), new { provider }) };
        properties.Items["AdminId"] = User.FindFirstValue(ClaimTypes.NameIdentifier);
        properties.Items["PageId"] = pageId ?? "";
        SocialAuthentication.UseInfrastructureCallbackBase(Request);
        return Challenge(properties, "Publish" + provider);
    }
    public async Task<IActionResult> Connected(string provider, CancellationToken ct)
    {
        if (!SocialPublishing.IsProvider(provider)) return BadRequest();
        var ticket = await HttpContext.AuthenticateAsync(SocialAuthentication.Cookie);
        await HttpContext.SignOutAsync(SocialAuthentication.Cookie);
        if (!ticket.Succeeded || ticket.Properties is null || !ticket.Properties.Items.TryGetValue("AdminId", out var adminId) || adminId != User.FindFirstValue(ClaimTypes.NameIdentifier)
            || ticket.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) != provider) return Forbid();
        var pageId = ticket.Properties.Items.TryGetValue("PageId", out var selectedPage) ? selectedPage ?? "" : "";
        var token = ticket.Properties.GetTokenValue("access_token");
        if (string.IsNullOrEmpty(token)) return BadRequest();
        try
        {
            (string Token, string Name) resolved;
            if (provider == "LinkedInPersonal")
            {
                var profile = await social.ResolvePersonalAsync(token, ct);
                pageId = profile.Id;
                resolved = (token, profile.Name);
            }
            else resolved = await social.ResolvePageAsync(provider, pageId, token, ct);
            if (SocialPublishing.IsLinkedIn(provider))
                await social.ValidateLinkedInGrantAsync(provider, token, ct);
            var connection = await db.SocialConnections.FindAsync([provider], ct);
            if (connection is null) { connection = new SocialConnection { Provider = provider }; db.SocialConnections.Add(connection); }
            connection.Enabled = true;
            connection.PageId = pageId; connection.PageName = resolved.Name;
            connection.ProtectedToken = social.Protect(resolved.Token); connection.ConnectedAt = DateTimeOffset.UtcNow;
            connection.ExpiresAt = DateTimeOffset.TryParse(ticket.Properties.GetTokenValue("expires_at"), out var expires) ? expires : null;
            db.Audit.Add(new AuditEntry { Actor = User.Identity?.Name ?? "Admin", Action = "Integration.Connected", Detail = provider + ":" + pageId });
            await db.SaveChangesAsync(ct);
            TempData["Notice"] = "IntegrationConnected";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            // Never log the exception message or provider response: either can contain tokens.
            var code = ex is InvalidOperationException && ex.Message is
                "IntegrationPageInvalid" or "IntegrationPageUnavailable" or "IntegrationPermissionMissing"
                or "IntegrationProviderRejected" or "IntegrationNotConfigured"
                ? ex.Message : "IntegrationUnavailable";
            HttpContext.RequestServices.GetRequiredService<ILogger<IntegrationsController>>()
                .LogWarning("SocialConnection Provider {Provider} Stage ResolveAndSave Code {Code} ExceptionType {ExceptionType} TraceId {TraceId}",
                    provider, code, ex.GetType().Name, HttpContext.TraceIdentifier);
            TempData["Notice"] = code;
        }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Disconnect(string provider, CancellationToken ct)
    {
        if (!SocialPublishing.IsProvider(provider)) return BadRequest();
        await db.SocialConnections.Where(x => x.Provider == provider).ExecuteDeleteAsync(ct);
        db.Audit.Add(new AuditEntry { Actor = User.Identity?.Name ?? "Admin", Action = "Integration.Disconnected", Detail = provider });
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}
