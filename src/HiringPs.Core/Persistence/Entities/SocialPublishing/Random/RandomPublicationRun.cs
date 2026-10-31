using System.ComponentModel.DataAnnotations;
namespace HiringPs.Core;

/// <summary>حجز دائم لموعد يومي؛ يمنع تكرار الموعد أو اختيار نفس الوظيفة للوجهة مرة أخرى.</summary>
public sealed class RandomPublicationRun
{
    /// <summary>هوية الموعد ومحاولة النشر المشتركة مع SocialPublications.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>الوجهة التي يخصها الموعد.</summary>
    [MaxLength(20)] public string Provider { get; set; } = "";
    /// <summary>اليوم المحلي الفلسطيني للجدول.</summary>
    public DateOnly Day { get; set; }
    /// <summary>رقم المنشور داخل اليوم، ابتداء من 1.</summary>
    public int Slot { get; set; }
    /// <summary>الموعد المحدد محولًا إلى UTC.</summary>
    public DateTimeOffset ScheduledAt { get; set; }
    /// <summary>وقت تثبيت الحجز بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>الوظيفة المحجوزة؛ فارغ إذا فات الموعد أو لم توجد وظيفة مؤهلة.</summary>
    public long? OpportunityId { get; set; }
    /// <summary>هوية الحساب وقت الحجز لمنع إرسال الموعد إلى حساب بديل بعد تغيير الربط.</summary>
    [MaxLength(100)] public string PageId { get; set; } = "";
    /// <summary>حالة التنفيذ أو عدم حسم نتيجة الاتصال.</summary>
    public RandomPublicationState State { get; set; }
    /// <summary>رمز نتيجة آمن دون مفاتيح أو ردود مزود خام.</summary>
    [MaxLength(200)] public string ResultCode { get; set; } = "";
}
