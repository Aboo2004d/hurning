using System.ComponentModel.DataAnnotations;

namespace HiringPs.Web.Models;

/// <summary>طلب منح اشتراك بالبريد؛ لا يُطلب من المسؤول إدخال معرّف المستخدم.</summary>
public sealed class SubscriptionGrantInput
{
    /// <summary>بريد الحساب المراد منحه الاشتراك.</summary>
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    /// <summary>نسخة الباقة المختارة.</summary>
    [Range(1, int.MaxValue)] public int VersionId { get; set; }
    /// <summary>مدة مخصصة اختيارية بدل مدة الباقة.</summary>
    [Range(1, 3650)] public int? Days { get; set; }
    /// <summary>السبب الإداري الذي يحفظ في التدقيق.</summary>
    [Required, StringLength(1500)] public string Reason { get; set; } = "";
}
