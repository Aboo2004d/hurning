using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>تفويض حساب أو صفحة نشر؛ يحفظ رمز الوصول مشفرًا.</summary>
public sealed class SocialConnection
{
    /// <summary>وجهة الربط أو النشر؛ يميز المؤسسة عن الحساب الشخصي.</summary>
    [Key, MaxLength(20)]
    public string Provider { get; set; } = "";
    /// <summary>يسمح باستخدام السجل أو التكامل؛ تعطيله لا يحذف البيانات السابقة.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>معرّف الصفحة أو الحساب لدى مزود النشر.</summary>
    [MaxLength(100)]
    public string PageId { get; set; } = "";
    /// <summary>اسم الصفحة أو الحساب عند إتمام الربط.</summary>
    [MaxLength(300)]
    public string PageName { get; set; } = "";
    /// <summary>رمز وصول مشفر بمفاتيح Data Protection؛ لا يعرض للمستخدم أو يسجل في السجلات.</summary>
    public string ProtectedToken { get; set; } = "";
    /// <summary>وقت انتهاء صلاحية السجل أو التفويض؛ لا يقبل الاستخدام بعده.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>وقت إتمام ربط الحساب أو الصفحة بالتطبيق.</summary>
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
}
