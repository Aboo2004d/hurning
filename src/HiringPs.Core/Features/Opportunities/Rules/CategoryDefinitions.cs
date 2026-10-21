namespace HiringPs.Core;

/// <summary>مرجع واحد للصيغ المعروفة بين عرض الفلاتر ومطابقة مجالات التنبيهات.</summary>
public static class CategoryDefinitions
{
    public static IReadOnlyList<CategoryDefinitionDto> All { get; } = Array.AsReadOnly<CategoryDefinitionDto>(
[
        new("business", "إدارة أعمال", "Business administration", ["ادارة اعمال", "ادارة الاعمال", "ادارة واعمال", "business administration"]),
        new("administration", "إدارة", "Administration", ["ادارة", "administration", "management"]),
        new("it", "تكنولوجيا المعلومات", "Information technology", ["تكنولوجيا المعلومات", "information technology", "it"]),
        new("health", "الصحة", "Health", ["الصحة", "صحة", "health"]),
        new("sales", "مبيعات", "Sales", ["مبيعات", "المبيعات", "sales"]),
        new("accounting", "محاسبة", "Accounting", ["محاسبة", "المحاسبة", "accounting", "finance"]),
        new("education", "التعليم", "Education", ["تعليم", "التعليم", "education", "educations", "the education"]),
        new("nursing", "التمريض", "Nursing", ["تمريض", "التمريض", "nursing", "التمريض والقبالة"]),
        new("procurement", "توريد وشراء", "Procurement and supply", ["توريد وشراء", "المشتريات", "procurement"]),
        new("services", "خدمات عامة", "General services", ["خدمات عامة", "general services"]),
        new("engineering", "هندسة", "Engineering", ["هندسة", "الهندسة", "engineering"]),
        new("archiving", "أرشفة", "Archiving", ["ارشفة", "الارشفة", "archiving"]),
        new("security", "الأمن", "Security", ["امن", "الامن", "security"]),
        new("projects", "إدارة المشاريع", "Project management", ["ادارة المشاريع", "project management"]),
        new("social", "العمل الاجتماعي", "Social work", ["العمل الاجتماعي", "social work"]),
        new("media", "إعلام", "Media", ["اعلام", "الاعلام", "media"]),
        new("food-security", "أمن غذائي وسبل عيش", "Food security and livelihoods", ["امن غذائي وسبل عيش", "food security and livelihoods"]),
        new("warehouse", "إدارة مستودعات", "Warehouse management", ["ادارة مستودعات", "warehouse management"]),
        new("academic", "أكاديمي", "Academic", ["اكاديمي", "academic"]),
    ]);
}
