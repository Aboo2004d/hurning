namespace HiringPs.Core;

/// <summary>صف وحيد لتنسيق كاش الفرص بين العامل المحلي وخادم الموقع.</summary>
public sealed class PublicCatalogRevision
{
    /// <summary>المفتاح الثابت لنسخة كتالوج الفرص العامة، وقيمته 1.</summary>
    public int Id { get; set; }
    /// <summary>عداد يزداد ذريًا داخل معاملة تغيير الفرص؛ لا يستخدم وقت جهاز العامل للمقارنة.</summary>
    public long Version { get; set; }
}
