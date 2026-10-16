using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

// JSON owns the browser location. Playwright's driver reads its cache location
// through this process variable; callers never need to set it in a start script.
public static class BrowserSettings
{
    public static string? BrowsersPath(IConfiguration configuration) =>
        configuration["Browser:BrowsersPath"] is { Length: > 0 } path
            ? Path.GetFullPath(path, AppContext.BaseDirectory)
            : null;

    public static string? ExecutablePath(IConfiguration configuration) =>
        configuration["Browser:ExecutablePath"] is { Length: > 0 } path
            ? Path.GetFullPath(path, AppContext.BaseDirectory)
            : null;

    public static void Configure(IConfiguration configuration)
    {
        if (BrowsersPath(configuration) is { } path)
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", path);
    }

    public static Task<IPlaywright> CreateAsync(IConfiguration configuration)
    {
        Configure(configuration);
        return Playwright.CreateAsync();
    }
}
