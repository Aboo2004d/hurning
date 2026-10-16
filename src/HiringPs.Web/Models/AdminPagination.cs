using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Models;

// All administration tables share a fixed server-side page size.
public sealed class AdminPager
{
    public const int PageSize = 10;
    public int Page { get; init; } = 1;
    public int TotalRecords { get; init; }
    public string Parameter { get; init; } = "page";
    public string Anchor { get; init; } = "";
    public int Pages => TotalRecords == 0 ? 1 : (TotalRecords - 1) / PageSize + 1;

    public static int Normalize(int page, int count) =>
        Math.Clamp(page, 1, count == 0 ? 1 : (count - 1) / PageSize + 1);

    public string Link(HttpRequest request, int page)
    {
        // Preserve filters and the independent page numbers of other tables.
        var values = request
            .Query.Where(x => !string.Equals(x.Key, Parameter, StringComparison.OrdinalIgnoreCase))
            .SelectMany(x =>
                x.Value.Select(value => new KeyValuePair<string, string?>(x.Key, value))
            );
        var query = QueryString
            .Create(values)
            .Add(
                QueryString.Create(
                    Parameter,
                    page.ToString(System.Globalization.CultureInfo.InvariantCulture)
                )
            );
        return request.PathBase.Add(request.Path) + query.ToString() + Anchor;
    }
}

public sealed class AdminPage<T>
{
    public List<T> Items { get; init; } = [];
    public AdminPager Pager { get; init; } = new();
}

public static class AdminPagination
{
    public static IQueryable<T> Window<T>(this IOrderedQueryable<T> query, int page) =>
        query.Skip((Math.Max(1, page) - 1) * AdminPager.PageSize).Take(AdminPager.PageSize);

    public static async Task<AdminPage<T>> PageAsync<T>(
        this IOrderedQueryable<T> query,
        int page,
        CancellationToken ct,
        string parameter = "page",
        string anchor = ""
    )
    {
        var count = await query.CountAsync(ct);
        page = AdminPager.Normalize(page, count);
        return new()
        {
            Items = await query.Window(page).ToListAsync(ct),
            Pager = new()
            {
                Page = page,
                TotalRecords = count,
                Parameter = parameter,
                Anchor = anchor,
            },
        };
    }
}
