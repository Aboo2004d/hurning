using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TelegramMembersController(HiringDb db, IConfiguration configuration, IDataProtectionProvider protection, Microsoft.Extensions.Localization.IStringLocalizer<Labels>? labels = null) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Broadcasts = await db.TelegramMemberBroadcasts.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct);
        return View(await db.TelegramMemberSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct) ?? new TelegramMemberSetting());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(TelegramMemberSetting input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.SearchEnabled && !input.CityEnabled && !input.OrganizationEnabled && !input.CategoryEnabled)
            return BadRequest();
        var settings = await db.TelegramMemberSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (settings is null) { settings = new(); db.TelegramMemberSettings.Add(settings); }
        settings.SearchEnabled = input.SearchEnabled;
        settings.PaidOnly = input.PaidOnly;
        settings.CityEnabled = input.CityEnabled;
        settings.OrganizationEnabled = input.OrganizationEnabled;
        settings.CategoryEnabled = input.CategoryEnabled;
        new MembershipService(db).Audit(User.Identity?.Name ?? "Admin", "Telegram.SearchSettings",
            $"Enabled {settings.SearchEnabled}; PaidOnly {settings.PaidOnly}; City {settings.CityEnabled}; Organization {settings.OrganizationEnabled}; Category {settings.CategoryEnabled}");
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Compose(string? userId) => View(new TelegramMemberInput { UserId = userId });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(TelegramMemberInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.Id == Guid.Empty || !new TelegramMemberMessaging(db, configuration).Fits(input.Body))
        {
            ModelState.AddModelError("", labels?["TelegramMemberTemplateInvalid"].Value ?? "Invalid message template or expanded length.");
            return View("Compose", input);
        }
        input.UserId = string.IsNullOrWhiteSpace(input.UserId) ? null : input.UserId.Trim();
        var messages = new TelegramMemberMessaging(db, configuration);
        var audience = messages.Audience(input.PaidOnly, input.UserId, DateTimeOffset.UtcNow);
        ViewBag.Count = await audience.CountAsync(ct);
        var sample = await audience.OrderBy(x => x.UserId).FirstOrDefaultAsync(ct);
        ViewBag.Sample = sample is null ? "" : messages.Render(input.Body, sample);
        ViewBag.Approval = protection.CreateProtector("TelegramMembers.Preview.v1").ToTimeLimitedDataProtector()
            .Protect(JsonSerializer.Serialize(input), TimeSpan.FromMinutes(15));
        return View(input);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string approval, CancellationToken ct)
    {
        TelegramMemberInput? input;
        try { input = JsonSerializer.Deserialize<TelegramMemberInput>(protection.CreateProtector("TelegramMembers.Preview.v1")
            .ToTimeLimitedDataProtector().Unprotect(approval)); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or ArgumentException) { return BadRequest(); }
        if (input is null || !await new TelegramMemberMessaging(db, configuration).QueueAsync(input.Id, input.Body,
            input.PaidOnly, input.UserId, User.Identity?.Name ?? "Admin", ct)) return BadRequest();
        TempData["Notice"] = "TelegramMemberQueued";
        return RedirectToAction(nameof(Index));
    }
}
