namespace HiringPs.Core;

/// <summary>قائمة المجالات الرئيسية والتخصصات؛ الصيغ المعروفة وحدها تُوحّد دون تشابه إملائي تخميني.</summary>
public static class CategoryTaxonomy
{
    public static IReadOnlyList<CategoryFamilyDto> Families { get; } = Array.AsReadOnly<CategoryFamilyDto>(
    [
        new("administration", "الإدارة والموارد البشرية", "Administration and human resources", ["إدارة", "إدارة أعمال", "إدارة المشاريع", "الموارد البشرية", "أرشفة"]),
        new("finance", "المالية والمحاسبة", "Finance and accounting", ["محاسبة", "المالية", "التدقيق المالي"]),
        new("technology", "تكنولوجيا المعلومات", "Information technology", ["تكنولوجيا المعلومات", "البرمجة وتطوير البرمجيات", "الشبكات والدعم التقني", "البيانات والذكاء الاصطناعي", "الأمن السيبراني"]),
        new("health", "الصحة والرعاية الطبية", "Health and medical care", ["الصحة", "الطب", "التمريض", "الصيدلة", "المختبرات الطبية"]),
        new("education", "التعليم والتدريب", "Education and training", ["التعليم", "أكاديمي", "التدريب"]),
        new("engineering", "الهندسة", "Engineering", ["هندسة", "الهندسة المدنية", "الهندسة الكهربائية", "الهندسة الميكانيكية", "الهندسة المعمارية"]),
        new("commerce", "المبيعات والتسويق", "Sales and marketing", ["مبيعات", "التسويق", "خدمة العملاء"]),
        new("media", "الإعلام والتصميم", "Media and design", ["إعلام", "التصميم الجرافيكي", "الترجمة والتحرير"]),
        new("logistics", "المشتريات واللوجستيات", "Procurement and logistics", ["توريد وشراء", "إدارة مستودعات", "النقل والتوزيع"]),
        new("social", "العمل الاجتماعي والإنساني", "Social and humanitarian work", ["العمل الاجتماعي", "الدعم النفسي", "العمل الإنساني"]),
        new("agriculture", "الزراعة والأمن الغذائي", "Agriculture and food security", ["الزراعة", "أمن غذائي وسبل عيش"]),
        new("legal", "القانون", "Law", ["القانون"]),
        new("services", "المهن والخدمات", "Trades and services", ["خدمات عامة", "الأمن", "المهن الفنية", "الضيافة"]),
    ]);

    // Explicit synonyms describe categories; role terms are separately scoped to the opportunity's title.
    private static readonly (string Category, string[] Aliases, string[] Roles)[] Specialties =
    [
        ("الموارد البشرية", ["موارد بشرية", "human resources", "hr"], ["موظف موارد بشرية", "مسؤول موارد بشرية", "human resources officer", "hr officer"]),
        ("المالية", ["مالية", "finance", "مالية ومحاسبة", "finance and accounting", "financial management"], ["مدير مالي", "مسؤول مالي", "financial officer", "finance officer", "finance manager"]),
        ("التدقيق المالي", ["تدقيق مالي", "auditing", "audit"], ["مدقق حسابات", "مدقق مالي", "auditor"]),
        ("البرمجة وتطوير البرمجيات", ["برمجة", "تطوير البرمجيات", "software development", "programming", "software engineering"], ["مبرمج", "مطور برمجيات", "مهندس برمجيات", "مطور ويب", "software developer", "software engineer", "web developer", "programmer"]),
        ("الشبكات والدعم التقني", ["شبكات", "دعم تقني", "technical support", "networking"], ["مهندس شبكات", "فني شبكات", "مسؤول شبكات", "فني حاسوب", "network engineer", "technical support", "it support"]),
        ("البيانات والذكاء الاصطناعي", ["تحليل البيانات", "data science", "data analysis", "artificial intelligence"], ["محلل بيانات", "عالم بيانات", "data analyst", "data scientist", "machine learning engineer"]),
        ("الأمن السيبراني", ["امن سيبراني", "cybersecurity", "information security"], ["محلل امن سيبراني", "cybersecurity analyst", "information security officer"]),
        ("الطب", ["طب", "medicine", "medical"], ["طبيب", "طبيبة", "physician", "medical doctor", "dentist", "طبيب اسنان"]),
        ("الصيدلة", ["صيدلة", "pharmacy"], ["صيدلي", "صيدلانية", "pharmacist"]),
        ("المختبرات الطبية", ["مختبرات طبية", "medical laboratory"], ["فني مختبر طبي", "فني مختبرات طبية", "medical laboratory technician"]),
        ("التدريب", ["تدريب", "training"], ["مدربين", "مشرفين خبراء", "مدرب", "مدربة", "trainer"]),
        ("الهندسة المدنية", ["هندسة مدنية", "civil engineering"], ["مهندس مدني", "مهندسة مدنية", "civil engineer"]),
        ("الهندسة الكهربائية", ["هندسة كهربائية", "electrical engineering"], ["مهندس كهرباء", "مهندس كهربائي", "electrical engineer"]),
        ("الهندسة الميكانيكية", ["هندسة ميكانيكية", "mechanical engineering"], ["مهندس ميكانيك", "mechanical engineer"]),
        ("الهندسة المعمارية", ["هندسة معمارية", "architecture"], ["مهندس معماري", "مهندسة معمارية", "architect"]),
        ("التسويق", ["تسويق", "marketing", "digital marketing"], ["مسؤول تسويق", "موظف تسويق", "اخصائي تسويق", "marketing officer", "marketing specialist"]),
        ("خدمة العملاء", ["خدمة زبائن", "customer service", "customer support"], ["موظف خدمة عملاء", "موظف خدمة زبائن", "customer service", "call center agent"]),
        ("التصميم الجرافيكي", ["تصميم جرافيك", "graphic design"], ["مصمم جرافيك", "مصمم جرافيكي", "graphic designer"]),
        ("الترجمة والتحرير", ["ترجمة", "تحرير", "translation", "editing"], ["مترجم", "مترجمة", "محرر", "translator", "editor"]),
        ("النقل والتوزيع", ["النقل والمواصلات", "نقل", "لوجستيات", "logistics", "transport"], ["سائق", "مندوب توزيع", "مسؤول توزيع", "driver", "distribution officer", "logistics officer"]),
        ("الدعم النفسي", ["دعم نفسي", "psychosocial support", "psychology"], ["اخصائي نفسي", "اخصائية نفسية", "مرشد نفسي", "psychologist", "psychosocial worker"]),
        ("العمل الإنساني", ["عمل انساني", "humanitarian work"], ["humanitarian officer", "مسؤول استجابة انسانية"]),
        ("الزراعة", ["زراعة", "agriculture"], ["مهندس زراعي", "عامل زراعي", "agricultural engineer", "agronomist"]),
        ("القانون", ["قانون", "law", "legal"], ["محامي", "محامية", "مستشار قانوني", "lawyer", "legal advisor", "legal officer"]),
        ("المهن الفنية", ["مهن فنية", "الصيانة", "صيانة", "maintenance", "skilled trades"], ["فني صيانة", "maintenance technician", "كهربائي", "سباك", "نجار", "لحام", "electrician", "plumber", "carpenter", "welder"]),
        ("الضيافة", ["ضيافة", "hospitality"], ["طباخ", "شيف", "نادل", "chef", "cook", "waiter"]),
        ("إدارة", ["إدخال البيانات", "data entry"], ["data entry operator", "data entry clerk", "مدخل بيانات", "موظف اداري", "كاتب", "clerk", "مساعد اداري", "مساعدة ادارية", "سكرتير", "سكرتيرة", "administrative assistant", "administrative officer", "secretary"]),
        ("تكنولوجيا المعلومات", ["هندسة الحاسوب", "computer engineering"], ["مهندس حاسوب", "computer engineer"]),
        ("إدارة المشاريع", ["متابعة وتقييم", "monitoring and evaluation", "meal"], ["meal monitor", "meal officer", "monitoring and evaluation officer"]),
        ("أكاديمي", ["بحث علمي", "scientific research"], ["موظف في مجال الابحاث", "research assistant", "study assistant"]),
        ("محاسبة", [], ["محاسب", "محاسبة", "accountant"]),
        ("التمريض", [], ["ممرض", "ممرضة", "nurse", "midwife", "قابلة"]),
        ("التعليم", [], ["education specialist", "اخصائي تعليم", "معلم", "معلمة", "مدرس", "مدرسة", "teacher"]),
        ("أكاديمي", [], ["استاذ مساعد", "محاضر", "assistant professor", "lecturer"]),
        ("مبيعات", [], ["مندوب مبيعات", "موظف مبيعات", "sales representative", "sales officer"]),
        ("إعلام", [], ["صحفي", "صحفية", "صحافي", "journalist", "media officer"]),
        ("إدارة المشاريع", [], ["مدير مشروع", "مدير مشاريع", "منسق مشروع", "project manager", "project coordinator"]),
        ("توريد وشراء", [], ["مسؤول مشتريات", "موظف مشتريات", "procurement officer", "purchasing officer"]),
        ("إدارة مستودعات", [], ["امين مستودع", "امين مخزن", "storekeeper", "warehouse manager"]),
        ("العمل الاجتماعي", [], ["اخصائي اجتماعي", "اخصائية اجتماعية", "social worker"]),
        ("الأمن", [], ["حارس امن", "حارس", "security guard"]),
        ("خدمات عامة", ["الخدمات", "خدمات", "services"], ["عامل نظافة", "عاملة نظافة", "cleaner"]),
    ];

    private static readonly IReadOnlyDictionary<string, string> Aliases = BuildAliases();
    public static string[] Labels => Families.SelectMany(x => x.Children.Prepend(x.Name)).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>يعيد التسمية الموحدة للصيغة المعروفة بالضبط؛ لا يصحح الأخطاء الإملائية بالتخمين.</summary>
    public static string? Canonical(string? value) => Aliases.GetValueOrDefault(CategorySimilarityRules.Normalize(value));

    /// <summary>يربط التسمية الرئيسية أو الفرعية المعروفة بعائلتها المقترحة دون تعديل انتماء معتمد.</summary>
    public static CategoryFamilyDto? Family(string? value)
    {
        var canonical = Canonical(value);
        return canonical is null ? null : Families.FirstOrDefault(x => x.Name == canonical || x.Children.Contains(canonical));
    }

    /// <summary>يستنتج من مسمى الدور أولًا؛ النص التفصيلي لا يُستخدم إلا عند احتوائه على اختصاص صريح وفريد.</summary>
    public static CategoryClassificationDto Classify(ExtractedOpportunity value, string roleSource = "", IEnumerable<string>? approvedCategories = null)
    {
        var explicitCategory = Canonical(value.Category);
        if (explicitCategory is not null) return new(explicitCategory, "");
        var approved = (approvedCategories ?? []).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)
            && CategoryRules.Key(x) == CategoryRules.Key(value.Category));
        if (approved is not null) return new(CategoryRules.Clean(approved), "");
        var title = CategorySimilarityRules.Normalize(value.Title);
        var matches = Specialties.Where(x => x.Roles.Any(term => Contains(title, term))).Select(x => x.Category).Distinct().ToArray();
        // Avoid treating a physician/nurse's incidental software skills as an IT position.
        var inferred = Unique(matches);
        if (inferred is not null) return new(inferred, "");
        if (matches.Length == 0)
        {
            var concepts = Families.SelectMany(x => x.Children).Where(label =>
                (value.Kind == OpportunityKind.Job || label != "التدريب")
                && (Contains(title, label) || Specialties.Any(s => s.Category == label && s.Aliases.Any(a => Contains(title, a)))))
                .Distinct().ToArray();
            inferred = Unique(concepts);
            if (inferred is not null) return new(inferred, "");
        }
        if (matches.Length == 0 && value.Kind == OpportunityKind.Job)
        {
            var duties = System.Text.RegularExpressions.Regex.Match(roleSource,
                @"(?:المهام(?:\s+والمسؤوليات)?|المسؤوليات|responsibilities|duties)(.*?)(?=المؤهلات|الشروط|المهارات|المتطلبات|qualifications|requirements|skills|آلية التقديم|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (duties.Success)
            {
                var text = CategorySimilarityRules.Normalize(duties.Groups[1].Value);
                (string Category, string[] Terms)[] signals =
                [
                    ("محاسبة", ["اعداد القيود المحاسبية", "تسجيل القيود المحاسبية", "اعداد القوائم المالية", "مراجعة الحسابات"]),
                    ("البرمجة وتطوير البرمجيات", ["تطوير البرمجيات", "تطوير تطبيقات الويب", "تطوير التطبيقات البرمجية", "develop software", "develop web applications"]),
                    ("التمريض", ["تقديم الرعاية التمريضية", "اعطاء الادوية للمرضى", "provide nursing care"]),
                    ("التعليم", ["تدريس الطلبة", "اعداد الخطط الدراسية", "تدريس الطلاب", "prepare lesson plans"]),
                    ("توريد وشراء", ["اعداد طلبات الشراء", "ادارة عمليات الشراء", "manage procurement"]),
                    ("التسويق", ["ادارة الحملات التسويقية", "اعداد الحملات التسويقية", "manage marketing campaigns"]),
                ];
                inferred = Unique(signals.Where(x => x.Terms.Any(term => Contains(text, term))).Select(x => x.Category).ToArray());
                if (inferred is not null) return new(inferred, "");
            }
        }
        return new("", "Category could not be reliably classified; select a field after reviewing the opportunity.");
    }

    /// <summary>يختار تخصصًا فريدًا أو عائلته عندما يدل المسمى على عدة تخصصات من العائلة نفسها.</summary>
    private static string? Unique(string[] labels)
    {
        if (labels.Length == 1) return labels[0];
        if (labels.Length == 0) return null;
        var families = labels.Select(Family).ToArray();
        return families.All(x => x is not null) && families.Select(x => x!.Key).Distinct().Count() == 1 ? Canonical(families[0]!.Name) : null;
    }

    /// <summary>يطابق عبارة بحدود الكلمات بعد توحيد الكتابة العربية والإنجليزية.</summary>
    private static bool Contains(string normalized, string term)
    {
        var text = " " + normalized + " ";
        var phrase = CategorySimilarityRules.Normalize(term);
        return text.Contains(" " + phrase + " ", StringComparison.Ordinal)
            || phrase.Any(c => c >= '\u0600' && c <= '\u06ff')
                && text.Contains(" و" + phrase + " ", StringComparison.Ordinal);
    }

    /// <summary>يبني الصيغ الدقيقة مع أولوية التخصص على اسم العائلة عند التطابق في الكتابة.</summary>
    private static IReadOnlyDictionary<string, string> BuildAliases()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var family in Families)
        {
            result[CategorySimilarityRules.Normalize(family.Name)] = family.Name;
            result[CategorySimilarityRules.Normalize(family.English)] = family.Name;
            foreach (var child in family.Children) result[CategorySimilarityRules.Normalize(child)] = child;
        }
        foreach (var category in CategoryDefinitions.All)
            foreach (var alias in category.Aliases.Append(category.Arabic).Append(category.English))
                result[CategorySimilarityRules.Normalize(alias)] = category.Arabic;
        foreach (var specialty in Specialties)
            foreach (var alias in specialty.Aliases.Append(specialty.Category))
                result[CategorySimilarityRules.Normalize(alias)] = specialty.Category;
        return result;
    }
}
