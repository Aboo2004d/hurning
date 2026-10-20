using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>سجل SocialPublication وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public sealed class SocialPublication
{
    /// <summary>معرّف نسخة قالب النشر التي استُخدمت لإنشاء النص.</summary>
    public long? TemplateVersionId { get; set; }
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public Guid Id { get; set; }
    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    public long OpportunityId { get; set; }
    /// <summary>الفرصة المرتبطة التي يحمّلها EF Core عند الحاجة.</summary>
    public Opportunity Opportunity { get; set; } = null!;
    /// <summary>النص المراد إرساله أو الذي أرسل فعليًا.</summary>
    [MaxLength(20)] public string Provider { get; set; } = "";
    [MaxLength(100)] public string PageId { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    public string Text { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    [MaxLength(500)] public string RemoteId { get; set; } = "";
    [MaxLength(1500)] public string Url { get; set; } = "";
    [MaxLength(256)] public string Actor { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    // Queue fields used by automatic providers (Telegram channel); manual posts keep the defaults.
    /// <summary>عدد محاولات التسليم أو الإخطار المحفوظة.</summary>
    public int Attempts { get; set; }
    /// <summary>أقرب وقت لمحاولة تسليم أو إخطار لاحقة وفق سياسة إعادة المحاولة.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }
    [MaxLength(200)] public string Error { get; set; } = "";
}

// Read-only probes and publishing use the same page identity and protected credentials.
