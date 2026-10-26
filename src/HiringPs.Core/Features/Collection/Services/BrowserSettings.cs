using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

// JSON owns the browser location. Playwright's driver reads its cache location
// through this process variable; callers never need to set it in a start script.
/// <summary>يجهز مسارات متصفحات Playwright وخيارات إطلاقها من الإعدادات.</summary>
public static class BrowserSettings
{
    /// <summary>يحسب مجلد متصفحات Playwright من الإعدادات.</summary>
    public static string? BrowsersPath(IConfiguration configuration)
    {
        var path = configuration["Browser:BrowsersPath"];
        if (string.IsNullOrWhiteSpace(path))
            path = "../.playwright-browsers";

        // Local build output is nested; deployed web and worker folders are siblings.
        if (path.Replace('\\', '/') == "../.playwright-browsers")
        {
            // A publish that carries the browsers inside the app folder (single-site deployment).
            var bundled = Path.Combine(AppContext.BaseDirectory, ".playwright-browsers");
            if (Directory.Exists(bundled))
                return bundled;
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "HiringPs.sln")))
                    return Path.Combine(directory.FullName, "src", ".playwright-browsers");
            }
        }

        return Path.GetFullPath(path, AppContext.BaseDirectory);
    }

    /// <summary>
    /// وسائط إضافية للمتصفح من Browser:Args (مثل --disable-gpu) لبيئات الاستضافة المقيدة،
    /// تُضبط من الإعدادات دون إعادة نشر.
    /// </summary>
    public static IEnumerable<string>? Args(IConfiguration configuration) =>
        configuration.GetSection("Browser:Args").Get<string[]>() is { Length: > 0 } args ? args : null;

    /// <summary>يحسب مسار المتصفح التنفيذي الاختياري من الإعدادات.</summary>
    public static string? ExecutablePath(IConfiguration configuration) =>
        configuration["Browser:ExecutablePath"] is { Length: > 0 } path
            ? Path.GetFullPath(path, AppContext.BaseDirectory)
            : null;

    /// <summary>يطبق إعدادات الخدمة وفق عقدها الحالي قبل التشغيل.</summary>
    public static void Configure(IConfiguration configuration)
    {
        if (BrowsersPath(configuration) is { } path)
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", path);
    }

    /// <summary>ينشئ محرك Playwright بعد تطبيق مسار المتصفح وإعدادات تشغيله.</summary>
    public static Task<IPlaywright> CreateAsync(IConfiguration configuration)
    {
        Configure(configuration);
        return Playwright.CreateAsync();
    }
}
