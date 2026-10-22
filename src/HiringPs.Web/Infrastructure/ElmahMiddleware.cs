using ElmahCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using HiringPs.Core;

namespace HiringPs.Web.Infrastructure;

/// <summary>يحمي واجهة ELMAH وجميع مواردها وواجهات بياناتها قبل الوصول إلى معالج الحزمة.</summary>
public static class ElmahMiddleware
{
    /// <summary>يركب ELMAH بعد المصادقة؛ يحظر غير المسؤول ويمنع تخزين أو فهرسة تفاصيل الأخطاء.</summary>
    public static IApplicationBuilder UseHiringElmah(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(ElmahLoggingRegistration.Path, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.ChallengeAsync(context);
                    return;
                }
                if (!context.User.IsInRole("Admin"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                // The bundled ELMAH UI uses Bootstrap/Vue dynamic style attributes.
                // Limit this allowance to the diagnostic UI; public pages keep their stricter policy.
                var bootstrapHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(
                    "window.$elmah_root = \"" + ElmahLoggingRegistration.Path + "\";")));
                context.Response.Headers.ContentSecurityPolicy = $"default-src 'self'; script-src 'self' 'sha256-{bootstrapHash}'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            }
            await next(context);
        });
        return app.UseElmah();
    }
}
