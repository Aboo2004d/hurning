using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Resources;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class SitePageInput
{
    public string Key { get; set; } = "";

    [MaxLength(200, ErrorMessage = "PageTitleLength")]
    public string TitleAr { get; set; } = "";

    [MaxLength(200, ErrorMessage = "PageTitleLength")]
    public string TitleEn { get; set; } = "";
    public string BodyAr { get; set; } = "";
    public string BodyEn { get; set; } = "";
    public byte[] Version { get; set; } = [];
}
