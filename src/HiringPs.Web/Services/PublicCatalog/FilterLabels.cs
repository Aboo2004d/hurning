// تسميات معروفة لقيم التصنيف ونوع التوظيف والخبرة المستخرجة من المصادر، مع الرجوع للنص الأصلي.
using System.Globalization;
using System.Text.RegularExpressions;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مكون FilterLabels ضمن مجال Services؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static partial class FilterLabels
{
    /// <summary>ينفذ Label وفق قواعد FilterLabels الحالية دون تغيير عقد المستدعين.</summary>
    private sealed record Label(string Key, string Arabic, string English, string[] Aliases);

    private static readonly Label[] Categories =
    [
        new("business", "إدارة أعمال", "Business administration", ["ادارة اعمال", "ادارة الاعمال", "ادارة واعمال", "business administration"]),
        new("administration", "إدارة", "Administration", ["ادارة", "administration", "management"]),
        new("it", "تكنولوجيا المعلومات", "Information technology", ["تكنولوجيا المعلومات", "information technology", "it"]),
        new("health", "الصحة", "Health", ["الصحة", "صحة", "health"]),
        new("sales", "مبيعات", "Sales", ["مبيعات", "المبيعات", "sales"]),
        new("accounting", "محاسبة", "Accounting", ["محاسبة", "المحاسبة", "accounting", "finance"]),
        new("education", "التعليم", "Education", ["تعليم", "التعليم", "education"]),
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
    ];

    private static readonly Label[] EmploymentTypes =
    [
        new("full-time", "دوام كامل", "Full-time", ["دوام كامل", "full time", "full-time", "fulltime"]),
        new("part-time", "دوام جزئي", "Part-time", ["جزئي", "دوام جزئي", "part time", "part-time"]),
        new("fixed-term", "عقد محدد المدة", "Fixed-term", ["fixed term", "fixed term appointment", "fixed-term", "limited duration contract", "limited duration contract ldc"]),
        new("temporary", "عقد مؤقت", "Temporary contract", ["عقد مؤقت", "عقد", "temporary appointment", "temporary"]),
        new("permanent", "دائم", "Permanent", ["دائم", "permanent"]),
        new("daily", "أجر يومي", "Daily wage", ["اجر يومي", "الاجر اليومي", "daily wage"]),
        new("internship", "تدريب عملي", "Internship", ["internship", "تدريب عملي"]),
    ];

    private static readonly Label[] ExperienceLevels =
    [
        new("unspecified", "غير محدد", "Not specified", ["غير محدد", "not specified"]),
        new("preferred", "تُفضّل الخبرة", "Experience preferred", ["يفضل من لديه خبرة", "فضل من لديه خبرة"]),
    ];

    /// <summary>ينفذ Category وفق قواعد FilterLabels الحالية دون تغيير عقد المستدعين.</summary>
    public static string Category(string value, bool english) => Display(Categories, value, english);

    /// <summary>ينفذ Employment وفق قواعد FilterLabels الحالية دون تغيير عقد المستدعين.</summary>
    public static string Employment(string value, bool english) => Display(EmploymentTypes, value, english);

    /// <summary>ينفذ Experience وفق قواعد FilterLabels الحالية دون تغيير عقد المستدعين.</summary>
    public static string Experience(string value, bool english)
    {
        var years = YearsPattern().Match(LocationSearch.Normalize(value));
        if (years.Success && int.TryParse(years.Groups[1].Value, out var count) && count is > 0 and < 50)
            return english
                ? count == 1 ? "1 year" : count.ToString(CultureInfo.InvariantCulture) + " years"
                : count switch
                {
                    1 => "سنة واحدة",
                    2 => "سنتان",
                    <= 10 => count.ToString(CultureInfo.InvariantCulture) + " سنوات",
                    _ => count.ToString(CultureInfo.InvariantCulture) + " سنة",
                };
        return Display(ExperienceLevels, value, english);
    }

    /// <summary>Groups raw values that share a known label; the option value is the first raw value.</summary>
    public static List<FilterOption> Options(
        IEnumerable<string> values,
        Func<string, bool, string> label,
        bool english
    ) =>
        values
            .GroupBy(value => label(value, english), StringComparer.OrdinalIgnoreCase)
            .Select(group => new FilterOption(group.OrderBy(x => x, StringComparer.Ordinal).First(), group.Key))
            .OrderBy(x => x.Label, StringComparer.Create(CultureInfo.CurrentCulture, true))
            .ToList();

    /// <summary>All raw values that display with the same label as <paramref name="selected"/>.</summary>
    public static string[] Equivalents(
        string selected,
        IEnumerable<string> values,
        Func<string, bool, string> label
    )
    {
        var target = label(selected, false);
        return values
            .Where(x => string.Equals(label(x, false), target, StringComparison.OrdinalIgnoreCase))
            .Append(selected)
            .Distinct()
            .ToArray();
    }

    /// <summary>ينفذ Display وفق قواعد FilterLabels الحالية دون تغيير عقد المستدعين.</summary>
    private static string Display(Label[] labels, string value, bool english)
    {
        var normalized = LocationSearch.Normalize(value);
        var match = labels.FirstOrDefault(x =>
            x.Aliases.Any(alias => LocationSearch.Normalize(alias) == normalized)
        );
        return match is null ? value : english ? match.English : match.Arabic;
    }

    /// <summary>ينفذ YearsPattern وفق قواعد FilterLabels الحالية دون تغيير عقد المستدعين.</summary>
    [GeneratedRegex(@"^(?:at least |minimum of )?(\d{1,2}) (?:years?|سنوات|سنة|سنين)$")]
    private static partial Regex YearsPattern();
}
