namespace HiringPs.Core;

/// <summary>شروط الفرص الحالية والمؤرشفة والخاصة بالتفاصيل العامة؛ لا تمنح صلاحية مشترك.</summary>
public static class PublicOpportunityScope
{
    /// <summary>المنشور الذي لم يتجاوز موعده وفق تاريخ فلسطين، أو بلا موعد نهائي.</summary>
    public static IQueryable<Opportunity> Current(IQueryable<Opportunity> query, DateOnly today) =>
        query.Where(x => x.Status == PublicationStatus.Published && (x.Deadline == null || x.Deadline >= today));

    /// <summary>يختار نطاق الرئيسية مع اعتبار المنشور المنتهي ضمن الأرشيف دون تعديل حالته في القاعدة.</summary>
    public static IQueryable<Opportunity> Listing(IQueryable<Opportunity> query, bool archive, DateOnly today) =>
        query.Where(x => archive
            ? x.Status == PublicationStatus.Archived || (x.Status == PublicationStatus.Published && x.Deadline != null && x.Deadline < today)
            : x.Status == PublicationStatus.Published && (x.Deadline == null || x.Deadline >= today));

    /// <summary>يحدد ما يجوز إدراجه في RSS وخريطة الموقع بناءً على إتاحة التفاصيل للعامة.</summary>
    public static IQueryable<Opportunity> PublicDetails(IQueryable<Opportunity> query, HiringDb db, DateOnly today) =>
        Current(query, today).Where(x => db.OpportunityAccessRules.Any(rule => rule.Kind == x.Kind && rule.PublicDetails));
}
