using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Resources;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public record SitePageViewModel(string Title, string Body)
{
    public IReadOnlyList<SiteQuestion> Questions { get; init; } = [];
    public string QuestionsTitle { get; init; } = "";
}
