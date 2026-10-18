using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class SiteQuestionsController : AdminControllerBase
{
    public SiteQuestionsController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    [HttpGet]
    public async Task<IActionResult> SiteQuestions(CancellationToken ct) =>
        View(
            SiteQuestionsContent.Read(
                await db
                    .SitePages.AsNoTracking()
                    .SingleOrDefaultAsync(p => p.Key == SiteQuestionsContent.Key, ct)
            )
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
        // These operations edit the draft only. Persist only on an explicit Save.
        if (command == "add")
        {
            input.Items.Add(new());
            ModelState.Clear();
            return View(input);
        }
        if (command is not null && command != "save")
        {
            var parts = command.Split(':');
            if (
                parts.Length != 2
                || !int.TryParse(parts[1], out var index)
                || index < 0
                || index >= input.Items.Count
            )
                return BadRequest();
            if (parts[0] == "remove")
                input.Items.RemoveAt(index);
            else if (parts[0] == "up" && index > 0)
                (input.Items[index - 1], input.Items[index]) = (
                    input.Items[index],
                    input.Items[index - 1]
                );
            else if (parts[0] == "down" && index + 1 < input.Items.Count)
                (input.Items[index + 1], input.Items[index]) = (
                    input.Items[index],
                    input.Items[index + 1]
                );
            ModelState.Clear();
            return View(input);
        }
        foreach (var item in input.Items)
        {
            item.QuestionAr = item.QuestionAr?.Trim() ?? "";
            item.AnswerAr = item.AnswerAr?.Trim() ?? "";
            item.QuestionEn = item.QuestionEn?.Trim() ?? "";
            item.AnswerEn = item.AnswerEn?.Trim() ?? "";
            if (
                string.IsNullOrEmpty(item.QuestionAr) != string.IsNullOrEmpty(item.AnswerAr)
                || string.IsNullOrEmpty(item.QuestionEn) != string.IsNullOrEmpty(item.AnswerEn)
            )
                ModelState.AddModelError("", "QuestionsPairRequired");
        }
        if (!ModelState.IsValid)
            return View(input);
        var page = await db.SitePages.SingleOrDefaultAsync(
            p => p.Key == SiteQuestionsContent.Key,
            ct
        );
        if (
            (page is not null && input.Version.Length == 0)
            || (page is null && input.Version.Length > 0)
        )
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }
        if (page is null)
        {
            page = new SitePage { Key = SiteQuestionsContent.Key };
            db.SitePages.Add(page);
        }
        else
            db.Entry(page).Property(p => p.Version).OriginalValue = input.Version;
        page.TitleAr = input.TitleAr.Trim();
        page.TitleEn = input.TitleEn.Trim();
        page.BodyAr = JsonSerializer.Serialize(input.Items);
        page.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("SiteQuestions.Update", $"Questions {input.Items.Count}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex is DbUpdateConcurrencyException
                || ex.InnerException
                    is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }
            )
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }
        TempData["Notice"] = "PageContentSaved";
        return RedirectToAction(nameof(SiteQuestions));
    }
}
