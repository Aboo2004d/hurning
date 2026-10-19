using System.Threading.RateLimiting;
using HiringPs.Core;
using HiringPs.Web;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ngrok and local reverse proxies connect from loopback, which ASP.NET trusts by default.
// Add the actual proxy IP in hosting configuration when it is not a loopback address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (
        var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>()
            ?? []
    )
        options.KnownProxies.Add(System.Net.IPAddress.Parse(address));
});
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
builder.Services.AddScoped<PaymentTraceWriter>();
builder.Services.AddScoped<WalletCheckoutService>();
builder.Services.AddScoped<PaymentGatewayRegistry>();
builder.Services.AddSingleton<PaymentEnvironment>();
builder.Services.AddScoped<PaymentToolProbes>();
builder.Services.AddSocialPublishing();
builder.Services.AddSocialAuthentication(builder.Configuration);
builder.Services.AddLahza();
builder.Services.AddJawwalPay();
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
if (
    !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"])
    && builder.Configuration["Authentication:Google:ClientId"] != "***"
    && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientSecret"])
    && builder.Configuration["Authentication:Google:ClientSecret"] != "***"
)
{
    builder
        .Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
            // Keep only the technical path fixed. Google middleware builds the absolute URI
            // from this request's public Scheme, Host (including port), and real mount path.
            options.CallbackPath = "/signin-google";
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.SaveTokens = false;
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                var diagnostic =
                    context.Properties?.Items.TryGetValue("GoogleAdminTest", out var test) == true
                    && test == "true";
                if (diagnostic && context.Properties!.Items.TryGetValue("GoogleTestReturnUrl", out var destination)
                    && !string.IsNullOrEmpty(destination))
                {
                    // LocalRedirect validates the protected return destination before redirecting.
                    return Results.LocalRedirect(
                        Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(destination, "googleTest", "failed")
                    ).ExecuteAsync(context.HttpContext);
                }
                context.Response.Redirect(
                    context.Request.PathBase
                        + (
                            diagnostic
                                ? "/Admin/Integrations/Index?googleTest=failed"
                                : "/Account/Login"
                        )
                );
                return Task.CompletedTask;
            };
        });
}
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Errors/Status?code=403";
    options.Cookie.Name = "HiringPs.Admin";
    // Language prefixes must share one login session and one logout cookie path.
    options.Cookie.Path = "/";
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
builder.Services.AddSiteAntiforgery(builder.Environment.IsDevelopment());
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

// Restore the public HTTPS scheme before redirects and OAuth correlation cookies are created.
app.UseForwardedHeaders();

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
            + "form-action 'self' https://checkout.lahza.io https://accounts.google.com https://www.facebook.com";
        await next();
    }
);

// ========================================
// 14. Middleware
// ========================================
app.UseExceptionHandler(
    new ExceptionHandlerOptions
    {
        ExceptionHandlingPath = "/Errors/Status",
        StatusCodeSelector = exception => exception is TimeoutException ? 504 : 500,
    }
);

// Authentication must capture the real mount path before language is added to PathBase.
// Changing PathBase inside a controller is too late for OAuth's original callback base.
app.UseAuthentication();
app.UseMiddleware<LanguagePathMiddleware>();
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
            "/Google",
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
