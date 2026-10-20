using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Models;

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
