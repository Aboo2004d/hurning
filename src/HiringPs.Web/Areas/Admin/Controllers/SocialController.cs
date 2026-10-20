using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SocialController(HiringDb db, SocialPublishing publishing, SocialTemplates templates, ILogger<SocialController> logger, SocialPublicationService? publicationService = null, SocialComposeReadService? readService = null) : Controller
{
    public async Task<IActionResult> Compose(long id, string provider, CancellationToken ct)
    {
        var reader = readService ?? new SocialComposeReadService(db, publishing, templates);
        var result = await reader.ReadAsync(id, provider, ct);
        if (result.Status == SocialComposeReadStatus.NotFound) return NotFound();
        if (result.Status != SocialComposeReadStatus.Ready || result.Data is not { } data) return BadRequest();
        var model = new SocialCompose
        {
            OpportunityId = data.OpportunityId, Provider = data.Provider, Opportunity = data.Opportunity,
            Text = data.Text, TemplateVersionId = data.TemplateVersionId, TemplateNumber = data.TemplateNumber,
            Link = data.Link, History = data.History, Destinations = data.Destinations,
            PreviouslyPublished = data.PreviouslyPublished,
        };
        if (TempData["SocialBatchResults"] is string results)
            model.PublishResults = JsonSerializer.Deserialize<Dictionary<string, string>>(results) ?? [];
        return Request.Headers["X-Requested-With"] == "fetch" ? PartialView("_Compose", model) : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Publish(SocialPublishInput input, CancellationToken ct)
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
            var outcome = await PublishTarget(new SocialPublishInput { Id = id, OpportunityId = input.OpportunityId,
                Provider = target, TemplateVersionId = input.TemplateVersionId, Text = input.Text,
                ConfirmRepeat = input.ConfirmRepeat }, ct);
            if (outcome is BadRequestResult) return outcome;
            publishResults[target] = TempData["Notice"] as string ?? "SocialAlreadyProcessed";
        }
        TempData["SocialBatchResults"] = JsonSerializer.Serialize(publishResults);
        return RedirectToAction(nameof(Compose), new { id = input.OpportunityId, provider = input.Provider });
    }

    /// <summary>يربط نتيجة أمر النشر برسالة الواجهة ووجهة العودة.</summary>
    private async Task<IActionResult> PublishTarget(SocialPublishInput input, CancellationToken ct)
    {
        var service = publicationService ?? new SocialPublicationService(db, publishing, logger);
        var result = await service.PublishAsync(new SocialPublicationCommandDto(input.Id,
            input.OpportunityId, input.Provider, input.TemplateVersionId, input.Text,
            input.ConfirmRepeat, User.Identity?.Name ?? "Admin"), ct);
        if (result.Notice is not null) TempData["Notice"] = result.Notice;
        return result.Route switch
        {
            SocialPublicationRoute.InvalidRequest => BadRequest(),
            SocialPublicationRoute.Integrations => RedirectToAction("Index", "Integrations"),
            _ => RedirectToAction(nameof(Compose), new { id = input.OpportunityId, provider = input.Provider }),
        };
    }
}
