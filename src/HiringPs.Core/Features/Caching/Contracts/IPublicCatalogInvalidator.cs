namespace HiringPs.Core;

/// <summary>يبطل نسخ الواجهة العامة في مضيف التطبيق بعد حفظ البيانات المؤثرة.</summary>
public interface IPublicCatalogInvalidator
{
    /// <summary>يبطل البيانات والصفحات العامة دون إعادة تنفيذ عملية الحفظ.</summary>
    void Invalidate();
}
