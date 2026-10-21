using System.Text.Encodings.Web;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Html;

/// <summary>Checks safe Arabic/Latin presentation and preservation of approved visual identity rules.</summary>
public static class PresentationChecks
{
    private static string Html(IHtmlContent value)
    {
        using var writer = new StringWriter();
        value.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    public static void Run()
    {
        var passed = 0;
        void Check(bool condition, string description)
        {
            if (!condition)
                throw new Exception("FAIL: " + description);
            passed++;
            Console.WriteLine("PASS: " + description);
        }
        Check(Html(BidiText.Render(null)) == "", "Missing presentation text renders empty");
        Check(Html(BidiText.Render("متدرب (GS Support Intern)")).Contains("<bdi dir=\"ltr\">(GS Support Intern)</bdi>"),
            "Latin parentheses remain isolated within Arabic text");
        var hostile = Html(BidiText.Render("وظيفة (English <img src=x onerror=alert(1)>) <script>bad()</script>"));
        Check(!hostile.Contains("<img") && !hostile.Contains("<script") && hostile.Contains("&lt;script&gt;"),
            "Announcement markup is encoded both inside and outside isolated text");
        Check(Html(BidiText.Render("<script>bad()</script>")) == HtmlEncoder.Default.Encode("<script>bad()</script>"),
            "Latin-only text remains encoded");
        Check(Html(BidiText.Render("فرصة (عربي English)")).IndexOf("<bdi", StringComparison.Ordinal) < 0,
            "Mixed Arabic parentheses retain their original direction");
        Check(BidiText.Direction("123 English", "ar") == "ltr" && BidiText.Direction("١٢٣ English", "ar") == "ltr"
            && BidiText.Direction("123 عربي", "en") == "rtl", "Direction follows the first letter rather than numeric prefixes");
        Check(BidiText.Direction("123", "ar-PS") == "rtl" && BidiText.Direction(null, "en") == "ltr",
            "Text without a letter uses the original language");
        var longInput = "عربي (" + new string('A', 50000);
        Check(Html(BidiText.Render(longInput)).Length > 50000, "Unclosed long Latin parentheses remain ordinary encoded text");
        foreach (var name in new[] { "مؤسسة تجريبية", "Company", "123 Company", "", "..." })
        {
            var avatar = OrganizationAvatarPresenter.Create(name);
            var first = name.FirstOrDefault(char.IsLetterOrDigit);
            Check(avatar.Tone == name.Sum(c => c) % 6
                && avatar.Initial == (first == default ? "•" : char.ToUpperInvariant(first).ToString()),
                "Organization identity preserves the approved color and initial: " + name);
        }
        var stats = new HomeStatsDto(new Dictionary<OpportunityKind, int> { [OpportunityKind.Job] = 10, [OpportunityKind.Training] = 8 }, 6, 3);
        Check(stats.Total == 18 && stats.Count(OpportunityKind.Job) == 10 && stats.Count(OpportunityKind.Tender) == 0,
            "Home statistics DTO preserves totals and missing-kind counts");
        Console.WriteLine($"Presentation checks passed: {passed}");
    }
}
