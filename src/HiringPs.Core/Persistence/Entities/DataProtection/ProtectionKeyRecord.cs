using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HiringPs.Core;

/// <summary>مفتاح حماية مشترك بين الويب والعامل؛ محتواه سري ولا يعرض في واجهات الإدارة.</summary>
[Table("DataProtectionKeys")]
public sealed class ProtectionKeyRecord
{
    /// <summary>معرف السجل؛ تبقى المفاتيح القديمة لفك البيانات السابقة.</summary>
    public long Id { get; set; }
    /// <summary>اسم المفتاح أو سجل الإلغاء الذي يولده إطار حماية البيانات.</summary>
    [MaxLength(256)] public string FriendlyName { get; set; } = "";
    /// <summary>تعريف المفتاح بصيغة XML؛ يعامل كبيانات اعتماد حساسة.</summary>
    public string Xml { get; set; } = "";
    /// <summary>وقت إضافة السجل UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
