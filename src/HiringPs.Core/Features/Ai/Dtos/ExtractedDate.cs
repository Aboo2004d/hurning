using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>DTO لموعد مستخلص مع نوعه ودليله قبل اعتماده ضمن فرصة.</summary>
public class ExtractedDate
{
    public OpportunityDateKind Kind { get; set; }
    public DateOnly Date { get; set; }
    public string Label { get; set; } = "";
    public string Evidence { get; set; } = "";
}
