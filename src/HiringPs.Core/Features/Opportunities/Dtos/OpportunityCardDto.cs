namespace HiringPs.Core;

/// <summary>حقول عرض بطاقة فرصة عامة؛ لا تحتوي كيانات EF أو إعدادات مصدر أو ملاحظات مراجعة داخلية.</summary>
public record OpportunityCardDto
{
    /// <summary>معرّف الفرصة المستخدم في روابط التفاصيل والحفظ.</summary>
    public long Id
    {
        get; init;
    }
    /// <summary>نوع الفرصة لتحديد الشارة وصلاحية التفاصيل.</summary>
    public OpportunityKind Kind
    {
        get; init;
    }
    /// <summary>العنوان بلغته الأصلية.</summary>
    public string Title { get; init; } = "";
    /// <summary>اسم الجهة المعروض.</summary>
    public string Organization { get; init; } = "";
    /// <summary>لغة العنوان والملخص لاتجاه النص.</summary>
    public string OriginalLanguage { get; init; } = "ar";
    /// <summary>الملخص المعروض في البطاقة والخلاصة.</summary>
    public string Summary { get; init; } = "";
    /// <summary>وصف مواقع الفرصة.</summary>
    public string Locations { get; init; } = "";
    /// <summary>التصنيف الذي تعرضه البطاقة.</summary>
    public string Category { get; init; } = "";
    /// <summary>نوع عقد العمل إن كانت الفرصة وظيفة.</summary>
    public string EmploymentType { get; init; } = "";
    /// <summary>المرجع المنشور إن توفر.</summary>
    public string ReferenceNumber { get; init; } = "";
    /// <summary>آخر موعد المثبت؛ لا يتضمن ساعة أو منطقة زمنية.</summary>
    public DateOnly? Deadline
    {
        get; init;
    }
    /// <summary>الحالة المستخدمة لتمييز السجل المؤرشف أو غير المتاح.</summary>
    public PublicationStatus Status
    {
        get; init;
    }
    /// <summary>وقت الإضافة UTC؛ يحوّل للعرض بتوقيت فلسطين.</summary>
    public DateTimeOffset CreatedAt
    {
        get; init;
    }
    /// <summary>تاريخ النشر الأصلي المثبت إن توفر.</summary>
    public DateTimeOffset? SourcePublishedAt
    {
        get; init;
    }
    /// <summary>تاريخ آخر تعديل أصلي مثبت إن توفر.</summary>
    public DateTimeOffset? SourceModifiedAt
    {
        get; init;
    }
}
