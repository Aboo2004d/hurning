using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>تحكم إداري في إتاحة بوابة الدفع للمبيعات الجديدة؛ لا يلغي التحقق من دفعات سابقة.</summary>
public sealed class PaymentGatewaySetting
{
    /// <summary>المفتاح النصي الثابت للبوابة أو الإعداد.</summary>
    [Key, MaxLength(30)]
    public string Key { get; set; } = "";
    /// <summary>يسمح باستخدام السجل أو التكامل؛ تعطيله لا يحذف البيانات السابقة.</summary>
    public bool Enabled { get; set; }
}
