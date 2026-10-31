using System.ComponentModel.DataAnnotations;
namespace HiringPs.Core;

/// <summary>إعدادات النشر العشوائي اليومية المستقلة لوجهة نشر واحدة.</summary>
public sealed class RandomPublicationSetting
{
    /// <summary>مفتاح الوجهة: Facebook أو LinkedInPersonal أو LinkedIn.</summary>
    [Key, MaxLength(20)] public string Provider { get; set; } = "";
    /// <summary>يسمح ببدء مواعيد جديدة؛ لا يلغي طلبًا أرسل بالفعل للمزود.</summary>
    public bool Enabled { get; set; }
    /// <summary>عدد المنشورات اليومية؛ يجب أن يطابق عدد الأوقات.</summary>
    public int DailyCount { get; set; } = 2;
    /// <summary>أقصى أيام متبقية حتى انتهاء التقديم؛ الفرص بلا نهاية مسموحة أيضًا.</summary>
    public int MaxRemainingDays { get; set; } = 7;
    /// <summary>قائمة أوقات HH:mm بتوقيت فلسطين وبترتيب المنشورات.</summary>
    public string TimesJson { get; set; } = "[\"09:00\",\"09:05\"]";
    /// <summary>رمز التزامن لمنع الكتابة فوق تعديل إداري أحدث.</summary>
    [Timestamp] public byte[] Version { get; set; } = [];
}
