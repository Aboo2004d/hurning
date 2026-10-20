using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>محتوى صفحة ثابتة بالعربية والإنجليزية يمكن للإدارة تحريره.</summary>
public class SitePage
{
    /// <summary>المفتاح النصي الثابت للبوابة أو الإعداد.</summary>
    [Key, MaxLength(40)]
    public string Key { get; set; } = "";

    /// <summary>عنوان المحتوى في الواجهة العربية.</summary>
    [MaxLength(200)]
    public string TitleAr { get; set; } = "";

    /// <summary>عنوان المحتوى في الواجهة الإنجليزية.</summary>
    [MaxLength(200)]
    public string TitleEn { get; set; } = "";
    /// <summary>نص المحتوى العربي.</summary>
    public string BodyAr { get; set; } = "";
    /// <summary>نص المحتوى الإنجليزي.</summary>
    public string BodyEn { get; set; } = "";
    /// <summary>وقت آخر تحديث للسجل.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
