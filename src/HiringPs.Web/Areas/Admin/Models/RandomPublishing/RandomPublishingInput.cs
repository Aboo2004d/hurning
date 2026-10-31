using System.ComponentModel.DataAnnotations;
namespace HiringPs.Web.Models;
/// <summary>مدخلات إعداد وجهة واحدة؛ الأوقات تعتمد توقيت فلسطين.</summary>
public sealed class RandomPublishingInput
{
    [Required] public string Provider { get; set; } = "";
    public bool Enabled { get; set; }
    [Range(1, 24)] public int DailyCount { get; set; } = 2;
    [Range(0, 3650)] public int MaxRemainingDays { get; set; } = 7;
    public string[] Times { get; set; } = [];
    public string? Version { get; set; } = "";
}
