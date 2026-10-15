using System.Threading.RateLimiting;
using HiringPs.Core;
using HiringPs.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================
// Database
// ======================================

var connectionString = builder.Configuration
    .GetConnectionString("Hiring")
    ?? throw new InvalidOperationException(
        "Missing ConnectionStrings:Hiring.");

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
    throw new InvalidOperationException(
        "Missing Ai:ApiKey.");

if (string.IsNullOrWhiteSpace(aiModel))
    throw new InvalidOperationException(
        "Missing Ai:Model.");

// ======================================
// Application Services
// ======================================

builder.Services.AddScoped<OpportunityService>();
builder.Services.AddScoped<CollectionPipeline>();

builder.Services.AddSingleton<UrlPolicy>();

builder.Services.AddScoped<BrowserCollector>();
builder.Services.AddScoped<AttachmentReader>();

// ======================================
// Gemini HTTP Client
// ======================================

builder.Services.AddHttpClient<AiExtractor>();

// ========================================
// 5. Identity Configuration
// ========================================

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<HiringDb>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.Cookie.Name = "HiringPs.Admin";
    options.Cookie.HttpOnly = true;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});


// ========================================
// 6. MVC Configuration
// ========================================

builder.Services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    options.Filters.Add(
        new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
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

    options.AddPolicy("contact", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("public", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});


// ========================================
// 9. Data Protection
// ========================================

var keyPath = builder.Configuration["DataProtection:KeyPath"];

var protection = builder.Services
    .AddDataProtection()
    .SetApplicationName("HiringPs");

if (!string.IsNullOrWhiteSpace(keyPath))
{
    protection.PersistKeysToFileSystem(
        new DirectoryInfo(keyPath));
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
    var db = scope.ServiceProvider
        .GetRequiredService<HiringDb>();

    await db.Database.MigrateAsync();

    await SeedData.SeedAsync(scope.ServiceProvider);
}


// ========================================
// 12. Production Configuration
// ========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}


// ========================================
// 13. Security Headers
// ========================================

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    context.Response.Headers["Referrer-Policy"] =
        "strict-origin-when-cross-origin";

    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    await next();
});


// ========================================
// 14. Middleware
// ========================================

app.UseRequestLocalization(
    new RequestLocalizationOptions()
        .SetDefaultCulture("ar")
        .AddSupportedCultures("ar", "en")
        .AddSupportedUICultures("ar", "en"));

app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();


// ========================================
// 15. Routes
// ========================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;