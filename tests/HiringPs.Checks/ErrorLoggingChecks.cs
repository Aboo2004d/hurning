using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using HiringPs.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class ErrorLoggingChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string description)
        {
            if (!value) throw new Exception(description);
            count++;
        }
        var mvcServices = new ServiceCollection();
        mvcServices.AddLogging();
        mvcServices.AddControllersWithViews();
        using var mvc = mvcServices.BuildServiceProvider();
        var validator = mvc.GetRequiredService<Microsoft.AspNetCore.Mvc.ModelBinding.Validation.IObjectModelValidator>();
        Microsoft.AspNetCore.Mvc.ActionContext ValidateFilter(SystemErrorFilterDto filter)
        {
            var action = new Microsoft.AspNetCore.Mvc.ActionContext(new DefaultHttpContext { RequestServices = mvc },
                new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
            validator.Validate(action, null, "filter", filter);
            return action;
        }
        Check(ValidateFilter(new()).ModelState.IsValid, "MVC accepts empty record filters without throwing");
        Check(ValidateFilter(new("trace", "Web", "Error")).ModelState.IsValid, "MVC accepts valid filters");
        Check(!ValidateFilter(new(new string('x', 201))).ModelState.IsValid, "MVC enforces search maximum");
        Check(!ValidateFilter(new(Application: new string('x', 33))).ModelState.IsValid, "MVC enforces application maximum");
        Check(!ValidateFilter(new(Level: new string('x', 17))).ModelState.IsValid, "MVC enforces severity maximum");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Integrations:Google:ClientSecret"] = "known-private-value",
            ["ConnectionStrings:Hiring"] = "Server=.;Database=test;Password=sql-private-value"
        }).Build();
        var sanitizer = new ErrorLogSanitizer(config);
        Check(!sanitizer.Clean("failed known-private-value", 8000).Contains("known-private-value"), "Configuration secrets redacted");
        Check(!sanitizer.Clean("Password=sql-private-value;", 8000).Contains("sql-private-value"), "Password assignments redacted");
        Check(!sanitizer.Clean("Bearer abc.def.ghi", 8000).Contains("abc.def.ghi"), "Bearer tokens redacted");
        Check(sanitizer.Clean("https://host/callback?code=private&state=secret", 8000) == "https://host/callback", "URL query omitted");
        Check(!sanitizer.Clean("\"access_token\": \"dynamic-private-token\"", 8000).Contains("dynamic-private-token"), "JSON token redacted");
        Check(!sanitizer.Clean("https://api.telegram.org/bot12345:dynamic-secret/sendMessage", 8000).Contains("dynamic-secret"), "Telegram URL token redacted");
        Check(sanitizer.Clean(new string('a', 10000), 8000).Length == 8000, "Text bounded");
        var context = new DefaultHttpContext();
        context.Request.Path = "/ar/Account/Login";
        context.Request.QueryString = new QueryString("?password=never-collected");
        context.Request.Headers.Cookie = "session=never-collected";
        var accessor = new HttpContextAccessor { HttpContext = context };
        using var provider = new DatabaseErrorLogging(config, accessor, sanitizer, "Web");
        var logger = provider.CreateLogger("Tests");
        var queue = (Channel<ApplicationError>)typeof(DatabaseErrorLogging)
            .GetField("queue", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(provider)!;
        logger.LogInformation("ordinary event");
        logger.LogWarning("ordinary warning");
        Check(!queue.Reader.TryRead(out _), "Ordinary logs excluded");
        using var activity = new Activity("request").Start();
        logger.LogError(new InvalidOperationException("known-private-value"), "Login failed");
        Check(queue.Reader.TryRead(out var entry), "Error queued without database IO");
        Check(entry!.TraceId == activity.Id, "Reference matches displayed Activity ID");
        Check(entry.Path == "/ar/Account/Login", "Only request path collected");
        Check(!entry.Details.Contains("known-private-value") && entry.Details.Contains("InvalidOperationException"), "Exception type retained and secret removed");
        Check(entry.At.Offset == TimeSpan.Zero, "UTC storage");
        logger.LogWarning(new IOException("failed"), "Handled failure");
        Check(queue.Reader.TryRead(out _), "Warning exceptions captured");
        logger.LogCritical("fatal");
        Check(queue.Reader.TryRead(out _), "Critical messages captured");
        accessor.HttpContext = null;
        logger.LogError("worker failure");
        Check(queue.Reader.TryRead(out entry) && entry.Path == "", "Non-HTTP logs supported");
        for (var i = 0; i < 2050; i++) logger.LogError("burst");
        Check(queue.Reader.Count == 2048, "Queue is bounded and producers never block");
        using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlServer("Server=.;Database=metadata").Options);
        var entity = db.Model.FindEntityType(typeof(ApplicationError))!;
        Check(entity.GetTableName() == "ApplicationErrors", "Dedicated table mapped");
        Check(entity.GetIndexes().Any(i => i.Properties.Single().Name == "TraceId"), "Reference index mapped");
        var reader = new SystemErrorReadService(db);
        var sql = reader.Query(new SystemErrorFilterDto("trace-probe", "Web", "Error",
            new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21))).ToQueryString();
        Check(sql.Contains("ORDER BY") && sql.Contains("[At] DESC") && sql.Contains("[Id] DESC"), "Stable SQL ordering");
        Check(sql.Contains("DECLARE @search_contains") && sql.Contains("[TraceId]") && sql.Contains("[Path]"), "Search and filters translated and parameterized");
        Check(!sql.Contains("[Details]"), "Large exception details excluded from list queries");
        var access = typeof(HiringPs.Web.Areas.Admin.Controllers.SystemErrorsController)
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Check(access?.Roles == "Admin", "Diagnostic pages restricted to administrators");
        var cache = typeof(HiringPs.Web.Areas.Admin.Controllers.SystemErrorsController)
            .GetCustomAttribute<Microsoft.AspNetCore.Mvc.ResponseCacheAttribute>();
        Check(cache?.NoStore == true, "Diagnostic pages cannot be cached");
        config["ConnectionStrings:Hiring"] = "unsupported-setting=true";
        using var failing = new DatabaseErrorLogging(config, accessor, sanitizer, "Worker");
        failing.CreateLogger("Failure").LogError("Preserve original failure");
        failing.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        failing.StopAsync(shutdown.Token).GetAwaiter().GetResult();
        Check(!shutdown.IsCancellationRequested, "Invalid database configuration cannot crash or recurse in logger");
        Console.WriteLine($"Error logging: {count} checks passed; no database or external calls.");
    }
}
