using HiringPs.Core;

/// <summary>Exercises classification boundaries, canonical naming and draft policy without AI requests.</summary>
internal static class CategoryClassificationChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        CategoryClassificationDto Classify(string title, string category = "", string source = "", OpportunityKind kind = OpportunityKind.Job) =>
            CategoryTaxonomy.Classify(new ExtractedOpportunity { Title = title, Category = category, Kind = kind }, source);
        Check(Classify("مساعد إداري").Category == "إدارة", "infer administrative field when AI leaves category empty");
        Check(Classify("محاسب").Category == "محاسبة", "infer accounting from role");
        Check(Classify("Civil Engineer").Category == "الهندسة المدنية", "preserve engineering specialty");
        Check(Classify("مهندس كهرباء").Category == "الهندسة الكهربائية", "electrical specialty remains distinct from civil");
        Check(Classify("software engineer").Category == "البرمجة وتطوير البرمجيات", "software engineering belongs to technology");
        Check(Classify("مهندس زراعي").Category == "الزراعة", "agricultural engineering belongs to agriculture");
        Check(Classify("ممرض", source: "المهارات: استخدام البرمجيات وتحليل البيانات").Category == "التمريض", "incidental technical skills do not change nursing field");
        Check(Classify("موظف", source: "المهام والمسؤوليات: إعداد القيود المحاسبية وإعداد القوائم المالية. المؤهلات: استخدام برامج الحاسوب").Category == "محاسبة", "infer from explicit duties for generic title");
        Check(Classify("موظف", source: "المهارات: إعداد القيود المحاسبية").Category == "", "skills alone do not establish professional duties");
        Check(Classify("موظف", source: "مقر الشركة: جامعة تدرس المحاسبة").Category == "", "employer sector alone cannot assign a field");
        Check(Classify("مساعد إداري ومحاسب").Category == "", "ambiguous cross-family role requires review");
        Check(Classify("مهندس مدني ومهندس كهرباء").Category == "هندسة", "multiple engineering specialties use their shared parent");
        Check(Classify("فرصة مميزة").ReviewReason.Length > 0, "unclassifiable opportunity is reviewed rather than assigned an invented field");
        Check(Classify("فرصة مميزة", "A completely new invented category").Category == "", "unapproved AI labels do not proliferate");
        Check(Classify("فرصة مميزة", "educatoin").Category == "", "spelling similarity alone never approves a new label");
        foreach (var alias in new[] { "education", "educations", "the education", "تعليم", "التعليم" })
            Check(Classify("فرصة", alias).Category == "التعليم", "normalize known education alias: " + alias);
        Check(Classify("فرصة", "Accounting").Category == "محاسبة", "English accounting uses one canonical label");
        Check(Classify("فرصة", "NURSING").Category == "التمريض", "case differences cannot create nursing duplicates");
        Check(Classify("دورة في البرمجة", kind: OpportunityKind.Training).Category == "البرمجة وتطوير البرمجيات", "training classified by subject rather than opportunity kind");
        Check(Classify("توريد أدوية", "pharmacy", kind: OpportunityKind.Tender).Category == "الصيدلة", "tender supports a source-derived model category");
        Check(CategoryTaxonomy.Classify(new() { Category = "حماية الطفل" }, approvedCategories: ["حماية الطفل"]).Category == "حماية الطفل", "administrator-approved custom fields remain available");
        Check(CategoryTaxonomy.Family("civil engineering")?.Key == "engineering"
            && CategoryTaxonomy.Family("هندسة كهربائية")?.Key == "engineering", "different engineering specialties share proposed main field");
        Check(CategoryTaxonomy.Family("محاسبة")?.Key != CategoryTaxonomy.Family("التمريض")?.Key, "unrelated professions stay in different families");
        Check(CategoryTaxonomy.Families.Select(x => x.Key).Distinct().Count() == CategoryTaxonomy.Families.Count,
            "main field identifiers are unique");
        Check(CategoryTaxonomy.Families.SelectMany(x => x.Children).Distinct().Count() == CategoryTaxonomy.Families.Sum(x => x.Children.Length),
            "each canonical specialty belongs to one proposed family");
        foreach (var label in CategoryTaxonomy.Labels)
            Check(CategoryTaxonomy.Canonical(label) is not null && CategoryTaxonomy.Family(label) is not null, "canonical field resolves to a family: " + label);
        Check(!CategoryGroupMap.Empty.Matches("هندسة مدنية", "هندسة كهربائية"), "taxonomy proposals alone do not broaden alerts");
        Check(PromptDefaults.Current().Render().Contains(PromptDefaults.CategoryPolicyMarker)
            && PromptDefaults.Current().Render().Contains("Infer the professional field"), "new default prompt includes source-supported category inference");
        Check(!PromptDefaults.Initial().Render().Contains(PromptDefaults.CategoryPolicyMarker), "historical prompt remains immutable");
        Console.WriteLine($"Category classification: {count} checks passed without external requests.");
    }
}
