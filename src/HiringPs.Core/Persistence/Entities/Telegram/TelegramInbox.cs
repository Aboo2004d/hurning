using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>تحديث Telegram محفوظ للمعالجة اللاحقة ومنع تكرار تنفيذ الأحداث.</summary>
public sealed class TelegramInbox
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>حمولة تحديث Telegram المحفوظة للمعالجة؛ تعامل كبيانات شخصية ولا تُسجل كاملة.</summary>
    [MaxLength(32000)]
    public string Json { get; set; } = "";
    /// <summary>وقت استقبال التحديث من Telegram.</summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت انتهاء معالجة التحديث.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }
    /// <summary>نهاية حجز المعالجة لمنع تنافس أكثر من عامل على السجل.</summary>
    public DateTimeOffset? LockedUntil { get; set; }
    /// <summary>عدد محاولات التنفيذ أو التحقق المسجلة.</summary>
    public int Attempts { get; set; }
}
