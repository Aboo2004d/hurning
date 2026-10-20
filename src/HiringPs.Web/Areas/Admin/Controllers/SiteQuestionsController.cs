using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class SiteQuestionsController : AdminControllerBase
{
    public SiteQuestionsController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        SiteContentService content
    )
        : base(database, urls, settings) => this.content = content;

    private readonly SiteContentService content;

    [HttpGet]
    public async Task<IActionResult> SiteQuestions(CancellationToken ct) =>
        View(
            SiteQuestionsContent.Read(await content.ReadAsync(SiteQuestionsContent.Key, ct))
        );

    [HttpPost]
    [RequestSizeLimit(512 * 1024)]
    public async Task<IActionResult> SiteQuestions(
        SiteQuestionsInput input,
        string? command,
        CancellationToken ct
    )
    {
        NormalizeStrings(input);
        input.Items ??= [];
        input.Version ??= [];
        // Draft commands return the submitted values without writing a content version.
        if (command is not null && command != "save")
        {
            if (!HiringPs.Web.Services.Content.SiteQuestionsDraft.TryApply(input, command))
                return BadRequest();
            ModelState.Clear();
            return View(input);
        }
        var incomplete = HiringPs.Web.Services.Content.SiteQuestionsDraft.NormalizeAndCountIncompletePairs(input);
        for (var index = 0; index < incomplete; index++)
            ModelState.AddModelError("", "QuestionsPairRequired");
        if (!ModelState.IsValid)
            return View(input);
        var outcome = await content.SaveQuestionsAsync(new(
            SiteQuestionsContent.Key, input.TitleAr, JsonSerializer.Serialize(input.Items), input.TitleEn, null, input.Version),
            input.Items.Count, User.Identity?.Name ?? "Admin", ct);
        if (outcome == RecordUpdateOutcome.Conflict)
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }
        TempData["Notice"] = "PageContentSaved";
        return RedirectToAction(nameof(SiteQuestions));
    }
}
