using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

public class SitePage
{
    [Key, MaxLength(40)] public string Key { get; set; } = "";
    [MaxLength(200)] public string TitleAr { get; set; } = "";
    [MaxLength(200)] public string TitleEn { get; set; } = "";
    public string BodyAr { get; set; } = "";
    public string BodyEn { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Timestamp] public byte[] Version { get; set; } = [];
}
