using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TelegramComposeController(TelegramComposeService service) : Controller
{
    public async Task<IActionResult> Index(long id, CancellationToken ct)
    {
        var data = await service.ReadAsync(id, ct);
        return data is null ? NotFound() : View(data);
    }

    [HttpPost]
    public async Task<IActionResult> Publish(TelegramComposeInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var outcome = await service.QueueAsync(new(input.Id, input.OpportunityId, input.TemplateVersionId,
            input.Text, input.Targets, User.Identity?.Name ?? "Admin"), ct);
        if (outcome == TelegramComposeOutcome.Invalid) return BadRequest();
        TempData["Notice"] = outcome == TelegramComposeOutcome.Queued ? "TelegramManualQueued" : "TelegramManualAlreadyQueued";
        return RedirectToAction(nameof(Index), new { id = input.OpportunityId });
    }
}
