using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

// JSON owns the browser location. Playwright's driver reads its cache location
// through this process variable; callers never need to set it in a start script.
public static class BrowserSettings
{
    public static string? BrowsersPath(IConfiguration configuration)
    {
        var path = configuration["Browser:BrowsersPath"];
        if (string.IsNullOrWhiteSpace(path))
            path = "../.playwright-browsers";

        // Local build output is nested; deployed web and worker folders are siblings.
        if (path.Replace('\\', '/') == "../.playwright-browsers")
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "HiringPs.sln")))
                    return Path.Combine(directory.FullName, "src", ".playwright-browsers");
            }
        }

        return Path.GetFullPath(path, AppContext.BaseDirectory);
    }

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
