using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class SiteQuestionsInput
{
    [MaxLength(200, ErrorMessage = "PageTitleLength")]
    public string TitleAr { get; set; } = "كيف يعمل الموقع؟";
    [MaxLength(200, ErrorMessage = "PageTitleLength")]
    public string TitleEn { get; set; } = "How the site works";
    public List<SiteQuestionInput> Items { get; set; } = [];
    public byte[] Version { get; set; } = [];
}
