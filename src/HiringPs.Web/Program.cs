using System.Threading.RateLimiting;
using HiringPs.Core;
using HiringPs.Web;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<HiringPs.Web.Models.SiteSeo>();

// ======================================
// Database
// ======================================
var connectionString =
    builder.Configuration.GetConnectionString("Hiring")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Hiring.");
builder.Services.AddDbContext<HiringDb>(options =>
{
    options.UseSqlServer(connectionString);
});

// ======================================
// AI Configuration Validation
// ======================================
var aiApiKey = builder.Configuration["Ai:ApiKey"];
var aiModel = builder.Configuration["Ai:Model"];
if (string.IsNullOrWhiteSpace(aiApiKey))
    throw new InvalidOperationException("Missing Ai:ApiKey.");
if (string.IsNullOrWhiteSpace(aiModel))
    throw new InvalidOperationException("Missing Ai:Model.");

// ======================================
// Application Services
// ======================================
builder.Services.AddScoped<OpportunityService>();
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<MemberVerification>();
builder.Services.AddScoped<PendingVerificationCookie>();
builder.Services.AddScoped<PaymentEvidenceStore>();
builder.Services.AddScoped<PaymentReconciliation>();
builder.Services.AddScoped<PaymentGatewayRegistry>();
builder
    .Services.AddHttpClient<TelegramClient>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .RemoveAllLoggers();
builder.Services.AddScoped<CollectionPipeline>();
builder.Services.AddSingleton<UrlPolicy>();
builder.Services.AddScoped<BrowserCollector>();
builder.Services.AddScoped<AttachmentReader>();

// ======================================
// Gemini HTTP Client
// ======================================
builder.Services.AddHttpClient<AiExtractor>();
builder.Services.AddHttpClient<SourceSetupAssistant>(client =>
    client.Timeout = TimeSpan.FromSeconds(75)
);

// ========================================
// 5. Identity Configuration
// ========================================
builder
    .Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireUppercase = false;
    })
    .AddEntityFrameworkStores<HiringDb>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Errors/Status?code=403";
    options.Cookie.Name = "HiringPs.Admin";
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (
            context.Principal?.Identity?.IsAuthenticated != true
            || context.Principal.IsInRole("Admin")
        )
            return;
        var id = context
            .Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?.Value;
        var db = context.HttpContext.RequestServices.GetRequiredService<HiringDb>();
        var eligible = await db.MemberProfiles.AnyAsync(
            x => x.UserId == id && x.Activated && !x.Suspended,
            context.HttpContext.RequestAborted
        );
        if (!eligible)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// ========================================
// 6. MVC Configuration
// ========================================
builder.Services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

// ========================================
// 7. Localization
// ========================================
builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

// ========================================
// 8. Rate Limiting
// ========================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy(
        "contact",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromMinutes(10),
                    QueueLimit = 0,
                }
            )
    );
    options.AddPolicy(
        "public",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 8,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
});

// ========================================
// 9. Data Protection
// ========================================
var keyPath = builder.Configuration["DataProtection:KeyPath"];
var protection = builder.Services.AddDataProtection().SetApplicationName("HiringPs");
if (!string.IsNullOrWhiteSpace(keyPath))
{
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}

// ========================================
// 10. Build Application
// ========================================
var app = builder.Build();

// ========================================
// 11. Database Migrations & Seed
// ========================================
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(scope.ServiceProvider);
}

// ========================================
// 12. Production Configuration
// ========================================
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ========================================
// 13. Security Headers
// ========================================
app.Use(
    async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; "
            + "script-src 'self'; "
            + "style-src 'self' https://fonts.googleapis.com; "
            + "font-src 'self' https://fonts.gstatic.com; "
            + "img-src 'self' data:; "
            + "frame-ancestors 'none'; "
            + "base-uri 'self'; "
            + "form-action 'self'";
        await next();
    }
);

// ========================================
// 14. Middleware
// ========================================
app.UseMiddleware<LanguagePathMiddleware>();
app.UseExceptionHandler(
    new ExceptionHandlerOptions
    {
        ExceptionHandlingPath = "/Errors/Status",
        StatusCodeSelector = exception => exception is TimeoutException ? 504 : 500,
    }
);
app.UseRequestLocalization(SiteLocalization.Options());

// Capture privacy before LanguagePathMiddleware restores the external request path.
app.Use(
    async (context, next) =>
    {
        var privatePage = new[]
        {
            "/Admin",
            "/Account",
            "/Member",
            "/Registration",
            "/PasswordReset",
            "/integrations",
        }.Any(prefix =>
            context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)
        );
        context.Response.OnStarting(() =>
        {
            if (privatePage || context.Response.StatusCode >= 400)
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            if (privatePage)
                context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        await next();
    }
);
app.UseStatusCodePagesWithReExecute("/Errors/Status", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(
    async (context, next) =>
    {
        if (
            context.User.Identity?.IsAuthenticated == true
            && !context.User.IsInRole("Admin")
            && context.Items["Language"] is string language
            && !LanguageUrls.IsInfrastructure(context.Request.Path)
        )
        {
            var id = context
                .User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?.Value;
            var db = context.RequestServices.GetRequiredService<HiringDb>();
            await db
                .MemberProfiles.Where(x => x.UserId == id && x.Language != language)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(y => y.Language, language),
                    context.RequestAborted
                );
        }
        await next();
    }
);
app.UseAuthorization();

// ========================================
// 15. Routes
// ========================================
app.MapSiteRoutes();
app.Run();

public partial class Program;
