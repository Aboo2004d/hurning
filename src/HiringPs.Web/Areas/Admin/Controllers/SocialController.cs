using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

public sealed class SocialCompose
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long OpportunityId { get; set; }
    public string Provider { get; set; } = "";
    public Opportunity? Opportunity { get; set; }
    [Required, MaxLength(50000)] public string Text { get; set; } = "";
    public string Link { get; set; } = "";
    public bool PreviouslyPublished { get; set; }
    public bool ConfirmRepeat { get; set; }
    public List<SocialPublication> History { get; set; } = [];
}

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SocialController(HiringDb db, SocialPublishing publishing) : Controller
{
    public async Task<IActionResult> Compose(long id, string provider, CancellationToken ct)
    {
        if (!SocialPublishing.IsProvider(provider)) return BadRequest();
        var item = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (!SocialPublishing.Eligible(item, TextRules.PalestineToday())) return BadRequest();
        var history = await db.SocialPublications.AsNoTracking().Where(x => x.OpportunityId == id && x.Provider == provider).OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct);
        var model = new SocialCompose { OpportunityId = id, Provider = provider, Opportunity = item,
            Text = string.Join("\n", new[] { item.Title, item.Organization, item.Summary, item.Locations, item.Deadline?.ToString("yyyy-MM-dd") }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Link = publishing.OpportunityUrl(id, item.OriginalLanguage), History = history,
            PreviouslyPublished = await db.SocialPublications.AnyAsync(x => x.OpportunityId == id && x.Provider == provider && x.Status == "Published", ct) };
        return Request.Headers["X-Requested-With"] == "fetch" ? PartialView("_Compose", model) : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Publish(SocialCompose input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.Id == Guid.Empty || !SocialPublishing.IsProvider(input.Provider)) return BadRequest();
        SocialPublication attempt;
        // Reserve before sending: concurrent clicks/replays cannot perform duplicate external requests.
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var resource = "SocialPublish:" + input.OpportunityId + ":" + input.Provider;
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @lockResult int; EXEC @lockResult=sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @lockResult < 0 THROW 51000, 'Social publication busy', 1;", ct);
            var item = await db.Opportunities.SingleOrDefaultAsync(x => x.Id == input.OpportunityId, ct);
            if (item is null || !SocialPublishing.Eligible(item, TextRules.PalestineToday())) return BadRequest();
            if (await db.SocialPublications.AnyAsync(x => x.Id == input.Id, ct)) return RedirectToAction(nameof(Compose), new { id = input.OpportunityId, provider = input.Provider });
            if (await db.SocialPublications.AnyAsync(x => x.OpportunityId == item.Id && x.Provider == input.Provider && (x.Status == "Pending" || x.Status == "Unknown"), ct))
            { TempData["Notice"] = "SocialUnknown"; return RedirectToAction(nameof(Compose), new { id = item.Id, provider = input.Provider }); }
            if (!input.ConfirmRepeat && await db.SocialPublications.AnyAsync(x => x.OpportunityId == item.Id && x.Provider == input.Provider && x.Status == "Published", ct))
            { TempData["Notice"] = "SocialRepeatRequired"; return RedirectToAction(nameof(Compose), new { id = item.Id, provider = input.Provider }); }
            var connection = await db.SocialConnections.SingleOrDefaultAsync(x => x.Provider == input.Provider, ct);
            if (connection is null) { TempData["Notice"] = "IntegrationNotConfigured"; return RedirectToAction("Index", "Integrations"); }
            var text = input.Text.Trim();
            var link = publishing.OpportunityUrl(item.Id, item.OriginalLanguage);
            // The canonical link is appended on the server, outside the editable text.
            text += "\n\n" + link;
            if (text.Length > (input.Provider == "LinkedIn" ? 3000 : 60000))
            { TempData["Notice"] = "SocialTextTooLong"; return RedirectToAction(nameof(Compose), new { id = item.Id, provider = input.Provider }); }
            attempt = new SocialPublication { Id = input.Id, OpportunityId = item.Id, Provider = input.Provider,
                PageId = connection.PageId, Text = text, Actor = User.Identity?.Name ?? "Admin" };
            db.SocialPublications.Add(attempt);
            db.Audit.Add(new AuditEntry { Actor = attempt.Actor, Action = "Social.Reserved", Detail = attempt.Id.ToString() });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        // A disconnected browser must not cancel recording a provider's result.
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        try { await publishing.PublishAsync(attempt, budget.Token); }
        catch (InvalidOperationException) { attempt.Status = "Failed"; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or KeyNotFoundException or System.Security.Cryptography.CryptographicException) { attempt.Status = "Unknown"; }
        await db.SaveChangesAsync(CancellationToken.None);
        TempData["Notice"] = "Social" + attempt.Status;
        return RedirectToAction(nameof(Compose), new { id = input.OpportunityId, provider = input.Provider });
    }
}
