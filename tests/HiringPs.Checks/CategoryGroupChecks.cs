using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiringPs.Web.Areas.Admin.Controllers;

public static class CategoryGroupChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool value, string description) { if (!value) throw new Exception(description); passed++; }
        var labels = new[] { "education", "educations", "the education", "تعليم", "التعليم" };
        var approved = new CategoryGroupMap(labels.Select(label => (label, 1, "التعليم")));
        foreach (var label in labels)
        {
            Check(approved.Matches("education", label), "Approved variants match: " + label);
            Check(CategorySimilarityRules.Suggests("education", label), "Known variant can be suggested: " + label);
        }
        Check(!CategoryGroupMap.Empty.Matches("education", "تعليم"), "Unapproved known variants stay independent");
        Check(!approved.Matches("education", "educatoin"), "New typo stays independent until approved");
        Check(CategorySimilarityRules.Suggests("education", "educatoin"), "New typo can be suggested without assignment");
        Check(!CategorySimilarityRules.Suggests("abcde", "abcdf"), "Exactly 80 percent is not above the threshold");
        Check(CategorySimilarityRules.Suggests("programming", "programing"), "Single typo above threshold suggested");
        Check(!CategorySimilarityRules.Suggests("IT", "HR"), "Short abbreviations not merged by spelling");
        Check(!CategorySimilarityRules.Suggests("هندسة مدنية", "هندسة كهربائية"), "Shared category prefix does not hide a specialty difference");
        Check(!CategorySimilarityRules.Suggests("Programming", "Programming languages"), "Added qualifier does not become same category");
        Check(PreferenceLimitRules.Count(labels, approved) == 1, "One approved group consumes one slot");
        Check(PreferenceLimitRules.Count(["education", "تعليم"]) == 2, "Suggestions alone never reduce the quota count");
        Check(PreferenceLimitRules.AllowsAlerts(labels.Concat(["Engineering", "Accounting"]), 3, approved), "Three logical categories fit free cap");
        Check(!PreferenceLimitRules.AllowsAlerts(labels.Concat(["Engineering", "Accounting", "Health"]), 3, approved), "Fourth group or independent field still exceeds cap");
        Check(!PreferenceLimitRules.AllowsAlerts([], null, approved), "Approved groups do not make empty selections wildcard");
        Check(!approved.Matches("", "education") && !approved.Matches("education", "Engineering"), "Blank and unrelated categories do not match");
        var pref = new MemberPreference { Kinds = 15, CategoriesJson = "[\"education\"]", LocationsJson = "[]" };
        var item = new Opportunity { Category = "التعليم", Kind = OpportunityKind.Job };
        Check(!MembershipRules.Matches(pref, item), "Runtime alerts ignore similarity without approved map");
        Check(MembershipRules.Matches(pref, item, approved), "Runtime alerts use approved group");
        Check(!MembershipRules.Matches(pref, item, CategoryGroupMap.Empty), "Removing grouping immediately removes cross-label equivalence");
        Check(CategoryGroupMap.Key(new string('x', 500)).Length <= 200, "Long original names fit the unique key without truncating labels");
        Check(CategoryGroupMap.Key(new string('x', 500)) != CategoryGroupMap.Key("sha256:" + TextRules.Hash(new string('x', 500))),
            "Literal category names cannot impersonate hashed long keys");
        using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlServer("Server=.;Database=metadata").Options);
        var member = db.Model.FindEntityType(typeof(CategoryGroupMember))!;
        Check(member.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == "NormalizedKey"), "One group per category enforced in SQL schema");
        Check(db.Model.FindEntityType(typeof(CategoryGroup))!.FindProperty("Version")!.IsConcurrencyToken, "Group edits protected by rowversion");
        Check(!db.Database.HasPendingModelChanges(), "Category group migration matches model");
        var sql = new CategoryGroupReadService(db).Groups("education").Skip(10).Take(10).ToQueryString();
        Check(sql.Contains("OFFSET") && sql.Contains("CategoryGroupMembers"), "Group list and nested members translate to paginated SQL");
        var management = new CategoryGroupManagementService(db);
        Check(await management.SaveAsync(new(0, "", [], [], "Test"), default) == CategoryGroupSaveOutcome.Invalid, "Invalid approval rejected before SQL");
        Check(await management.DeleteAsync(1, [], "Test", default) == CategoryGroupSaveOutcome.Invalid, "Invalid removal rejected before SQL");
        var access = (AuthorizeAttribute)Attribute.GetCustomAttribute(typeof(CategoryGroupsController), typeof(AuthorizeAttribute))!;
        Check(access.Roles == "Admin", "Group administration restricted to Admin");
        foreach (var method in new[] { "Save", "Delete" })
            Check(Attribute.IsDefined(typeof(CategoryGroupsController).GetMethod(method)!, typeof(ValidateAntiForgeryTokenAttribute)), "CSRF required for " + method);
        Console.WriteLine($"Approved category groups: {passed} checks passed; no SQL connection or external requests.");
    }
}
