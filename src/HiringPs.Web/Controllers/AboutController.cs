using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

public class AboutController(SiteContentService content) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await ReadSitePageAsync("about", ct);
        var questions = SiteQuestionsContent.Read(
            await content.ReadAsync(SiteQuestionsContent.Key, ct)
        );
        var english =
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        return View(
            model with
            {
                Questions = SiteQuestionsContent.Localize(questions, english),
                QuestionsTitle = english ? questions.TitleEn : questions.TitleAr,
            }
        );
    }

    private async Task<SitePageViewModel> ReadSitePageAsync(string key, CancellationToken ct)
    {
        var page = await content.ReadAsync(key, ct);
        var language =
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? "en"
                : "ar";
        return SitePageDefaults.Resolve(key, page, language);
    }
}
