using HiringPs.Web.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HiringPs.Web.Infrastructure;

public static class SiteRoutes
{
    public static void MapSiteRoutes(this IEndpointRouteBuilder app)
    {
        app.MapControllerRoute(
            "admin-home",
            "Admin",
            new
            {
                area = "Admin",
                controller = "Admin",
                action = "Index",
            }
        );
        // Explicit form routes precede the legacy /Admin/Sources/{id?} patterns.
        foreach (var controller in new[] { "Plans", "Discounts", "Sources", "Opportunities" })
        foreach (var action in new[] { "Create", "Edit" })
            app.MapControllerRoute(
                "admin-form-" + controller + "-" + action,
                "Admin/" + controller + "/" + action + "/{id?}",
                new
                {
                    area = "Admin",
                    controller,
                    action,
                }
            );
        // Match tool commands before /Admin/Tools/{id?}; otherwise CheckTool
        // is treated as a dashboard id and no check is queued.
        foreach (var action in new[] { "CheckTool", "CheckTools", "CheckAi", "CheckIntegration" })
            app.MapControllerRoute(
                "admin-tools-" + action,
                "Admin/Tools/" + action,
                new { area = "Admin", controller = "Tools", action }
            );
        foreach (var pair in AdminActionRoutes.Controllers)
            app.MapControllerRoute(
                "admin-" + pair.Key,
                "Admin/" + pair.Key + "/{id?}",
                new
                {
                    area = "Admin",
                    controller = pair.Value,
                    action = pair.Key,
                }
            );
        app.MapControllerRoute(
            "areas",
            "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}"
        );
        // Legacy /Home/* URLs are permanent redirects, not controller routes, so they never
        // take part in link generation. PathBase already carries the language prefix here.
        app.MapGet(
            "Home/Details/{id:long}",
            (HttpContext context, long id) =>
                Results.Redirect(
                    context.Request.PathBase + "/Opportunities/Details/" + id + context.Request.QueryString,
                    permanent: true
                )
        );
        app.MapPost(
            "Home/Report/{id?}",
            (HttpContext context) =>
                Results.Redirect(
                    context.Request.PathBase + "/Opportunities/Report" + context.Request.QueryString,
                    permanent: true,
                    preserveMethod: true
                )
        );
        foreach (var page in new[] { "About", "Contact", "Privacy" })
            app.MapGet(
                "Home/" + page,
                (HttpContext context) =>
                    Results.Redirect(context.Request.PathBase + "/" + page, permanent: true)
            );
        app.MapControllerRoute(
            "opportunity-details",
            "Opportunities/Details/{id:long}/{slug?}",
            new { controller = "Opportunities", action = "Details" }
        );
        app.MapControllerRoute(
            "browse-city",
            "{kindSlug:" + KindSlugs.RouteConstraint + "}/city/{citySlug}",
            new { controller = "Browse", action = "City" }
        );
        app.MapControllerRoute(
            "browse-organization",
            "org/{orgSlug}",
            new { controller = "Browse", action = "Organization" }
        );
        app.MapControllerRoute(
            "language",
            "Home/Language",
            new { controller = "Language", action = "Switch" }
        );
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    }
}
