using System.Threading.RateLimiting;
using HiringPs.Core;
using HiringPs.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddHiring(builder.Configuration);
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o => { o.Password.RequiredLength = 12; o.Lockout.MaxFailedAccessAttempts = 5; o.User.RequireUniqueEmail = true; }).AddEntityFrameworkStores<HiringDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Login"; o.Cookie.Name = "HiringPs.Admin"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; });
builder.Services.AddControllersWithViews(o => { o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true; o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()); });
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
var keyPath = builder.Configuration["DataProtection:KeyPath"]; var protection = builder.Services.AddDataProtection().SetApplicationName("HiringPs");
if (!string.IsNullOrWhiteSpace(keyPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(keyPath)) throw new InvalidOperationException("Configure persistent protected DataProtection:KeyPath outside wwwroot.");
var app = builder.Build();
// Apply migrations and seed data automatically on startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HiringDb>();

    await db.Database.MigrateAsync();

    await SeedData.SeedAsync(scope.ServiceProvider);
}
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) => { context.Response.Headers["X-Content-Type-Options"] = "nosniff"; context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin"; context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"; await next(); });
app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("ar").AddSupportedCultures("ar", "en").AddSupportedUICultures("ar", "en"));
app.UseStaticFiles(); app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); app.Run();
public partial class Program;
