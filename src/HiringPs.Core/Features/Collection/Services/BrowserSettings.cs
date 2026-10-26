using Microsoft.Extensions.Configuration;
using System.ComponentModel;
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

    /// <summary>
    /// يشغل Edge المثبت أولًا، ثم Chromium المضمّن إذا تعذر إطلاق Edge.
    /// المسار التنفيذي الصريح يبقى تجاوزًا اختياريًا للتشخيص والبيئات المخصصة.
    /// لا يعيد تنفيذ عملية الجمع عند فشلها؛ البديل يخص إطلاق المتصفح فقط.
    /// </summary>
    public static async Task<IBrowser> LaunchAsync(
        IPlaywright runtime,
        IConfiguration configuration,
        float timeout = 20000
    ) => (await LaunchWithDetailsAsync(runtime, configuration, timeout)).Browser;

    /// <summary>يشغل المتصفح ويعيد هويته من مسار الإطلاق الفعلي بدل تخمينها من رقم الإصدار.</summary>
    public static async Task<BrowserLaunchResultDto> LaunchWithDetailsAsync(
        IPlaywright runtime,
        IConfiguration configuration,
        float timeout = 20000
    )
    {
        var options = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Timeout = timeout,
            Args = Args(configuration),
            ExecutablePath = ExecutablePath(configuration),
        };
        if (options.ExecutablePath is not null)
        {
            try
            {
                return new(await runtime.Chromium.LaunchAsync(options), "Custom executable", false);
            }
            catch (PlaywrightException ex)
            {
                throw new BrowserRuntimeException(IsDriverExit(ex) ? BrowserFailureStage.DriverConnection : BrowserFailureStage.CustomBrowserLaunch,
                    $"Executable={options.ExecutablePath}; {ex.Message}", ex);
            }
        }

        options.Channel = "msedge";
        try
        {
            return new(await runtime.Chromium.LaunchAsync(options), "Microsoft Edge", false);
        }
        catch (PlaywrightException edgeError)
        {
            if (IsDriverExit(edgeError))
                throw new BrowserRuntimeException(BrowserFailureStage.DriverConnection,
                    "Driver exited while launching Edge; Chromium cannot use the closed driver. "
                    + edgeError.Message + "; " + BrowserDriverDiagnostics.Context(BrowserDriverDiagnostics.Paths()), edgeError);
            // Null channel selects Playwright's matching bundled Chromium revision.
            options.Channel = null;
            try
            {
                return new(await runtime.Chromium.LaunchAsync(options), "Chromium", true);
            }
            catch (PlaywrightException chromiumError)
            {
                throw new BrowserRuntimeException(
                    IsDriverExit(chromiumError) ? BrowserFailureStage.DriverConnection : BrowserFailureStage.BrowserLaunch,
                    $"Edge launch failed: {edgeError.Message}\nChromium fallback failed: {chromiumError.Message}; "
                    + $"Chromium executable={runtime.Chromium.ExecutablePath}", chromiumError
                );
            }
        }
    }

    /// <summary>يطبق إعدادات الخدمة وفق عقدها الحالي قبل التشغيل.</summary>
    public static void Configure(IConfiguration configuration)
    {
        if (BrowsersPath(configuration) is { } path)
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", path);
    }

    /// <summary>ينشئ محرك Playwright بعد تطبيق مسار المتصفح وإعدادات تشغيله.</summary>
    public static async Task<IPlaywright> CreateAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        Configure(configuration);
        var paths = BrowserDriverDiagnostics.Paths();
        try
        {
            ct.ThrowIfCancellationRequested();
            BrowserDriverDiagnostics.ValidateBundle(paths);
            return await Playwright.CreateAsync();
        }
        catch (Exception ex) when (ex is PlaywrightException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            var probe = await BrowserDriverDiagnostics.ProbeAsync(paths, ct);
            throw new BrowserRuntimeException(BrowserFailureStage.DriverStartup,
                $"Original={ex.Message}; {probe}; {BrowserDriverDiagnostics.Context(paths)}", ex);
        }
    }

    private static bool IsDriverExit(PlaywrightException error)
    {
        // TargetClosedException is internal in Playwright .NET; inspect its stable type name and signal.
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current.GetType().Name == "TargetClosedException"
                && current.Message.Contains("Process exited", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
