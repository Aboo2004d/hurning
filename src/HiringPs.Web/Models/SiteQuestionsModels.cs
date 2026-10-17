using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class SiteQuestionInput
{
    public string QuestionAr { get; set; } = "";
    public string AnswerAr { get; set; } = "";
    public string QuestionEn { get; set; } = "";
    public string AnswerEn { get; set; } = "";
}

public sealed class SiteQuestionsInput
{
    [MaxLength(200, ErrorMessage = "PageTitleLength")]
    public string TitleAr { get; set; } = "كيف يعمل الموقع؟";
    [MaxLength(200, ErrorMessage = "PageTitleLength")]
    public string TitleEn { get; set; } = "How the site works";
    public List<SiteQuestionInput> Items { get; set; } = [];
    public byte[] Version { get; set; } = [];
}

public static class SiteQuestionsContent
{
    public const string Key = "about-questions";

    public static SiteQuestionsInput Read(SitePage? page)
    {
        if (page is not null)
            return new()
            {
                TitleAr = page.TitleAr,
                TitleEn = page.TitleEn,
                Items = JsonSerializer.Deserialize<List<SiteQuestionInput>>(page.BodyAr) ?? [],
                Version = page.Version,
            };
        // Preserve the current content until the administrator first saves the section.
        var ar = SiteFacts.Questions(false);
        var en = SiteFacts.Questions(true);
        return new()
        {
            Items = ar.Select((q, i) => new SiteQuestionInput
            {
                QuestionAr = q.Question, AnswerAr = q.Answer,
                QuestionEn = en[i].Question, AnswerEn = en[i].Answer,
            }).ToList(),
        };
    }

    public static IReadOnlyList<SiteQuestion> Localize(SiteQuestionsInput input, bool english) =>
        input.Items.Select(q => english
            ? new SiteQuestion(q.QuestionEn, q.AnswerEn)
            : new SiteQuestion(q.QuestionAr, q.AnswerAr))
        .Where(q => !string.IsNullOrWhiteSpace(q.Question) && !string.IsNullOrWhiteSpace(q.Answer))
        .ToArray();
}
