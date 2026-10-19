using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

public sealed class SocialCompose
{
    public long? TemplateVersionId { get; set; }
    public int TemplateNumber { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public long OpportunityId { get; set; }
    public string Provider { get; set; } = "";
    public string[]? Targets { get; set; }
    public List<SocialConnection> Destinations { get; set; } = [];
    public Dictionary<string, string> PublishResults { get; set; } = [];
    public Opportunity? Opportunity { get; set; }
    [Required, MaxLength(50000)] public string Text { get; set; } = "";
    public string Link { get; set; } = "";
    public bool PreviouslyPublished { get; set; }
    public bool ConfirmRepeat { get; set; }
    public List<SocialPublication> History { get; set; } = [];
}

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SocialController(HiringDb db, SocialPublishing publishing, SocialTemplates templates) : Controller
{
    public async Task<IActionResult> Compose(long id, string provider, CancellationToken ct)
    {
        if (!SocialPublishing.IsProvider(provider)) return BadRequest();
        var item = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (!SocialPublishing.Eligible(item, TextRules.PalestineToday())) return BadRequest();
        var family = SocialPublishing.IsLinkedIn(provider) ? new[] { "LinkedIn", "LinkedInPersonal" } : new[] { provider };
        var history = await db.SocialPublications.AsNoTracking().Where(x => x.OpportunityId == id && family.Contains(x.Provider)).OrderByDescending(x => x.CreatedAt).Take(40).ToListAsync(ct);
        var template = await templates.ResolveAsync(provider, item.Kind, ct);
        var model = new SocialCompose { OpportunityId = id, Provider = provider, Opportunity = item,
            Text = SocialTemplateText.Render(template.Body, item), TemplateVersionId = template.Id, TemplateNumber = template.Number,
            Link = publishing.OpportunityUrl(id, item.OriginalLanguage), History = history,
            Destinations = await db.SocialConnections.AsNoTracking().Where(x => family.Contains(x.Provider)).ToListAsync(ct),
            PreviouslyPublished = await db.SocialPublications.AnyAsync(x => x.OpportunityId == id && family.Contains(x.Provider) && x.Status == "Published", ct) };
        if (TempData["SocialBatchResults"] is string results)
            model.PublishResults = JsonSerializer.Deserialize<Dictionary<string, string>>(results) ?? [];
        return Request.Headers["X-Requested-With"] == "fetch" ? PartialView("_Compose", model) : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Publish(SocialCompose input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.Id == Guid.Empty || !SocialPublishing.IsProvider(input.Provider)) return BadRequest();
        var targets = (input.Targets ?? (SocialPublishing.IsLinkedIn(input.Provider) ? [] : [input.Provider])).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (targets.Length == 0 || targets.Length > 2 || targets.Any(x =>
            SocialPublishing.IsLinkedIn(input.Provider) ? !SocialPublishing.IsLinkedIn(x) : x != input.Provider)) return BadRequest();
        var item = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.OpportunityId, ct);
        if (item is null || !SocialPublishing.Eligible(item, TextRules.PalestineToday())) return BadRequest();
        var templateProvider = SocialPublishing.TemplateProvider(input.Provider);
        if (input.TemplateVersionId is not {} templateId || !await db.SocialTemplateVersions.AnyAsync(
            x => x.Id == templateId && x.Provider == templateProvider && (x.Scope == item.Kind.ToString() || x.Scope == "general"), ct)) return BadRequest();
        var publishResults = new Dictionary<string, string>();
        foreach (var target in targets)
        {
            // Stable per-destination keys protect each send when a multi-target form is replayed.
            var id = target == input.Provider ? input.Id
                : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(input.Id.ToString("N") + ":" + target)).AsSpan(0, 16));
            var outcome = await PublishTarget(new SocialCompose { Id = id, OpportunityId = input.OpportunityId,
                Provider = target, TemplateVersionId = input.TemplateVersionId, Text = input.Text,
                ConfirmRepeat = input.ConfirmRepeat }, ct);
            if (outcome is BadRequestResult) return outcome;
            publishResults[target] = TempData["Notice"] as string ?? "SocialAlreadyProcessed";
        }
        TempData["SocialBatchResults"] = JsonSerializer.Serialize(publishResults);
        return RedirectToAction(nameof(Compose), new { id = input.OpportunityId, provider = input.Provider });
    }

    private async Task<IActionResult> PublishTarget(SocialCompose input, CancellationToken ct)
    {
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
            if (connection is null || !connection.Enabled || connection.ExpiresAt <= DateTimeOffset.UtcNow) { TempData["Notice"] = "IntegrationNotConfigured"; return RedirectToAction("Index", "Integrations"); }
            var templateProvider = SocialPublishing.TemplateProvider(input.Provider);
            if (input.TemplateVersionId is not {} templateId || !await db.SocialTemplateVersions.AnyAsync(x => x.Id == templateId && x.Provider == templateProvider && (x.Scope == item.Kind.ToString() || x.Scope == "general"), ct)) return BadRequest();
            var text = input.Text.Trim();
            var link = publishing.OpportunityUrl(item.Id, item.OriginalLanguage);
            // The canonical link is appended on the server, outside the editable text.
            text = text.Replace(link, "", StringComparison.Ordinal).Trim() + "\n\n" + link;
            if (text.Length > (SocialPublishing.IsLinkedIn(input.Provider) ? 3000 : 60000))
            { TempData["Notice"] = "SocialTextTooLong"; return RedirectToAction(nameof(Compose), new { id = item.Id, provider = input.Provider }); }
            attempt = new SocialPublication { Id = input.Id, OpportunityId = item.Id, Provider = input.Provider,
                PageId = connection.PageId, TemplateVersionId = input.TemplateVersionId, Text = text, Actor = User.Identity?.Name ?? "Admin" };
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
