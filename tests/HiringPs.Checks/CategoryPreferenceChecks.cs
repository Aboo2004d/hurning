using HiringPs.Core;

public static class CategoryPreferenceChecks
{
    public static void Run()
    {
        var passed = 0;
        void Check(bool value, string description) { if (!value) throw new Exception(description); passed++; }
        Check(PreferenceLimitRules.Effective([]) == 3, "No active subscription has a cap of three");
        Check(PreferenceLimitRules.Effective([1]) == 1, "Active paid cap remains authoritative");
        Check(PreferenceLimitRules.Effective([2, 5]) == 5, "Active overlapping caps use highest entitlement");
        Check(PreferenceLimitRules.Effective([null]) is null, "Unlimited purchased cap remains unlimited");
        Check(!PreferenceLimitRules.AllowsAlerts([], null), "Unlimited does not imply all-category alerts");
        Check(!PreferenceLimitRules.AllowsAlerts(["", "   "], null), "Blank selections cannot enable alerts");
        Check(!PreferenceLimitRules.AllowsAlerts(["A", "B", "C", "D"], 3), "Excess old preferences do not bypass quota");
        Check(PreferenceLimitRules.AllowsAlerts(["A", "B", "C"], 3), "Exact quota is valid");
        Check(PreferenceLimitRules.Count(["إدارة  أعمال", " ادارة أعمال ", "IT", "it"]) == 2, "Equivalent formatting does not consume extra slots");
        Check(CategoryRules.Resolve(" ادارة  أعمال ", ["إدارة أعمال", "محاسبة"]) == "إدارة أعمال", "Existing canonical label reused");
        Check(CategoryRules.Resolve("Programing", ["Programming"]) == "Programing", "Fuzzy suggestion never rewrites user choice");
        Check(CategoryRules.Resolve(" علوم  البيانات ", []) == "علوم البيانات", "New labels normalize spaces");
        Check(CategoryRules.ValidNew("علوم البيانات") && !CategoryRules.ValidNew(" "), "New labels must be nonempty");
        Check(!CategoryRules.ValidNew(new string('x', 201)), "New label size is bounded");
        Check(!CategoryRules.ValidNew("label\0"), "Control characters rejected");
        var pref = new MemberPreference { Kinds = 15, CategoriesJson = "[]", LocationsJson = "[]" };
        var item = new Opportunity { Category = "Programming", Kind = OpportunityKind.Job };
        Check(!MembershipRules.Matches(pref, item), "No selected category rejects all opportunities");
        pref.CategoriesJson = "[\"Engineering\"]";
        Check(!MembershipRules.Matches(pref, item), "Unselected category rejected");
        pref.CategoriesJson = "[\" programming \" ]";
        Check(MembershipRules.Matches(pref, item), "Selected normalized category matches with unrestricted locations");
        item.Category = "Programming languages";
        Check(!MembershipRules.Matches(pref, item), "Partial category name cannot broaden alerts");
        item.Category = "";
        Check(!MembershipRules.Matches(pref, item), "Unclassified opportunity cannot match a chosen category");
        pref.CategoriesJson = "[\"ادارة أعمال\"]"; item.Category = "إدارة أعمال";
        Check(MembershipRules.Matches(pref, item), "Arabic orthographic equivalents match consistently");
        pref.LocationsJson = "[\"غزة\"]"; item.LocationSearchText = LocationSearch.Normalize("رام الله");
        Check(!MembershipRules.Matches(pref, item), "Location restriction remains enforced");
        pref.LocationsJson = "[]"; pref.Kinds = MembershipRules.Bit(OpportunityKind.Training);
        Check(!MembershipRules.Matches(pref, item), "Opportunity kind restriction remains enforced");
        Console.WriteLine($"Category and alert rules: {passed} checks passed; no database or external requests.");
    }
}
