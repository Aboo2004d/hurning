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
