using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class AiUsageTotals
{
    public int PromptVersionId { get; set; }
    public string Model { get; set; } = "";
    public int Requests { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Unfinished { get; set; }
    public int UnknownTokenRequests { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
}
