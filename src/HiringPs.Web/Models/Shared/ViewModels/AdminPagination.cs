using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Models;

public static class AdminPagination
{
    /// <summary>Applies a page window to a query whose read service already defines a stable ordering.</summary>
    public static IQueryable<T> Window<T>(this IQueryable<T> query, int page) =>
        query.Skip((Math.Max(1, page) - 1) * AdminPager.PageSize).Take(AdminPager.PageSize);

    /// <summary>Counts and materializes a page after projection. Callers must provide a stable order before pagination.</summary>
    public static async Task<AdminPage<T>> PageAsync<T>(
        this IQueryable<T> query,
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
