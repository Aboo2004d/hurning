using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

public static class AdminPaginationChecks
{
    public static void Run()
    {
        var passed = 0;
        void Check(bool condition, string description)
        {
            if (!condition)
                throw new Exception(description);
            passed++;
            Console.WriteLine("PASS " + description);
        }
        var ordered = Enumerable.Range(1, 25).AsQueryable().OrderBy(x => x);
        Check(
            ordered.Window(1).SequenceEqual(Enumerable.Range(1, 10)),
            "first page contains ten rows"
        );
        Check(
            ordered.Window(2).SequenceEqual(Enumerable.Range(11, 10)),
            "next page fetches the next ten rows"
        );
        Check(
            ordered.Window(3).SequenceEqual(Enumerable.Range(21, 5)),
            "last page has only remaining rows"
        );
        Check(new AdminPager { TotalRecords = 10 }.Pages == 1, "ten records need no navigation");
        Check(
            new AdminPager { TotalRecords = 11 }.Pages == 2,
            "eleventh record creates a second page"
        );
        Check(AdminPager.Normalize(-5, 25) == 1, "negative page is normalized");
        Check(
            AdminPager.Normalize(int.MaxValue, 25) == 3,
            "oversized page resolves to the last page"
        );
        Check(AdminPager.Normalize(5, 0) == 1, "empty table has a valid first page");

        var request = new DefaultHttpContext().Request;
        request.PathBase = "/hiring";
        request.Path = "/Admin/AiUsage";
        request.QueryString = new QueryString("?days=90&page=2&groupsPage=3&tag=a&tag=b");
        var pager = new AdminPager { Parameter = "groupsPage", Anchor = "#ai-totals" };
        var url = new Uri("https://example.test" + pager.Link(request, 4));
        var query = QueryHelpers.ParseQuery(url.Query);
        Check(
            query["days"] == "90" && query["page"] == "2" && query["groupsPage"] == "4",
            "filters and the other table's page survive navigation"
        );
        Check(query["tag"].Count == 2, "multi-value filters survive navigation");
        Check(
            url.AbsolutePath == "/hiring/Admin/AiUsage" && url.Fragment == "#ai-totals",
            "hosting subpath and table anchor survive navigation"
        );
        request.QueryString = new QueryString("?PAGE=9&status=NeedsReview&inspect=12");
        url = new Uri("https://example.test" + new AdminPager().Link(request, 2));
        query = QueryHelpers.ParseQuery(url.Query);
        Check(
            query["page"].Count == 1
                && query["page"] == "2"
                && query["status"] == "NeedsReview"
                && query["inspect"] == "12",
            "case-insensitive page replacement preserves review and inspected version"
        );

        using var db = new HiringDb(
            new DbContextOptionsBuilder<HiringDb>()
                .UseSqlServer(
                    "Server=(localdb)\\MSSQLLocalDB;Database=PaginationCheck;Trusted_Connection=True"
                )
                .Options
        );
        var sql = db
            .Opportunities.AsNoTracking()
            .Where(x => x.Status == PublicationStatus.NeedsReview)
            .OrderByDescending(x => x.Id)
            .Window(2)
            .ToQueryString();
        Check(
            sql.Contains("OFFSET") && sql.Contains("FETCH NEXT") && sql.Contains("= 10"),
            "SQL Server applies the ten-row limit and offset before materializing rows"
        );
        var grouped = db
            .AiUsage.AsNoTracking()
            .GroupBy(x => new { x.PromptVersionId, x.Model })
            .Select(g => new
            {
                g.Key.PromptVersionId,
                g.Key.Model,
                Total = g.Sum(x => x.TotalTokens),
            })
            .OrderByDescending(x => x.PromptVersionId)
            .ThenBy(x => x.Model)
            .Window(2)
            .ToQueryString();
        Check(
            grouped.Contains("GROUP BY") && grouped.Contains("FETCH NEXT"),
            "AI totals are grouped before paging, rather than summing only ten requests"
        );
        Console.WriteLine(
            $"{passed} administration pagination checks passed; no database connection or HTTP requests."
        );
    }
}
