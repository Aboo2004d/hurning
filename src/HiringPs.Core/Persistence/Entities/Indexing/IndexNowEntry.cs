// إبلاغ محركات البحث عبر IndexNow بتغير صفحات الفرص العامة، على دفعات من العامل وليس داخل طلبات الويب.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>سجل IndexNowEntry وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public sealed class IndexNowEntry
{
    /// <summary>معرف الفرصة المرتبطة بالسجل.</summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long OpportunityId { get; set; }
    /// <summary>الفرصة التي يخصها السجل.</summary>
    public Opportunity Opportunity { get; set; } = null!;
    /// <summary>وقت آخر تغيير يستدعي إخطار محركات البحث برابط الفرصة.</summary>
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;

    // Pending while ProcessedAt is null or older than QueuedAt.
    /// <summary>وقت معالجة آخر نسخة من التغيير المطلوب إخطارها.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    // Last accepted submission of the ar/en URLs to IndexNow.
    /// <summary>وقت آخر إرسال مؤكد إلى IndexNow.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    // Status seen when last processed; detects bulk changes that bypass SaveChanges.
    /// <summary>حالة نشر الفرصة عند آخر إخطار مؤكد.</summary>
    public PublicationStatus? SubmittedStatus { get; set; }
    /// <summary>أقرب وقت لمحاولة تسليم أو إخطار لاحقة وفق سياسة إعادة المحاولة.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }
    /// <summary>عدد محاولات التسليم أو الإخطار المحفوظة.</summary>
    public int Attempts { get; set; }
    /// <summary>آخر رمز HTTP ورد من خدمة الإخطار.</summary>
    public int? LastStatus { get; set; }

    /// <summary>رمز الخطأ المحفوظ دون أسرار أو ردود مزود كاملة.</summary>
    [MaxLength(200)]
    public string LastError { get; set; } = "";
}
