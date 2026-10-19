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
        foreach (var action in new[] { "Details", "Report" })
            app.MapControllerRoute(
                "legacy-opportunity-" + action,
                "Home/" + action + "/{id?}",
                new { controller = "Opportunities", action }
            );
        foreach (var page in new[] { "About", "Contact", "Privacy" })
            app.MapControllerRoute(
                "legacy-" + page,
                "Home/" + page,
                new { controller = page, action = "Index" }
            );
        app.MapControllerRoute(
            "language",
            "Home/Language",
            new { controller = "Language", action = "Switch" }
        );
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    }
}
