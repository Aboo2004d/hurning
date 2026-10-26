using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يفحص محرك Playwright المرفق تحت حساب التطبيق دون تثبيت أدوات أو تشغيل متصفح.</summary>
public static class BrowserDriverDiagnostics
{
    /// <summary>يحسب مسارات برنامج التشغيل بالطريقة نفسها التي تستخدمها حزمة Playwright .NET.</summary>
    public static BrowserDriverPathsDto Paths()
    {
        var root = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var platform = OperatingSystem.IsWindows() ? "win32_x64"
            : OperatingSystem.IsMacOS() ? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "darwin-arm64" : "darwin-x64")
            : RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        var nodeName = OperatingSystem.IsWindows() ? "node.exe" : "node";
        var nodeOverride = Environment.GetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH");
        BrowserDriverPathsDto At(string folder) => new(
            nodeOverride ?? Path.Combine(folder, ".playwright", "node", platform, nodeName),
            Path.Combine(folder, ".playwright", "package", "cli.js"));
        if (!string.IsNullOrEmpty(root)) return At(Path.GetFullPath(root));
        root = File.Exists(Path.Combine(AppContext.BaseDirectory, "Microsoft.Playwright.dll"))
            ? AppContext.BaseDirectory : Path.GetDirectoryName(typeof(Playwright).Assembly.Location) ?? AppContext.BaseDirectory;
        var result = At(root);
        return File.Exists(result.Node) || Directory.Exists(Path.Combine(root, ".playwright"))
            ? result : At(Path.GetFullPath(Path.Combine(root, "..", "..")));
    }

    /// <summary>يصف الحساب والمسارات دون تسجيل متغيرات البيئة أو بيانات الطلب الحساسة.</summary>
    public static string Context(BrowserDriverPathsDto paths) =>
        $"Node={paths.Node}; CLI={paths.EntryPoint}; Account={Environment.UserDomainName}\\{Environment.UserName}; "
        + $"Process={RuntimeInformation.ProcessArchitecture}; OS={RuntimeInformation.OSArchitecture}";

    /// <summary>يتحقق من ملفات الحزمة قبل بدء الاتصال؛ لا يغير المسارات ولا ينزّل ملفات ناقصة.</summary>
    public static void ValidateBundle(BrowserDriverPathsDto paths)
    {
        // A bare explicitly configured node command is resolved by the OS through PATH.
        if ((Path.IsPathRooted(paths.Node) || paths.Node.Contains(Path.DirectorySeparatorChar)) && !File.Exists(paths.Node))
            throw new FileNotFoundException("Playwright node executable is missing from the deployed bundle.", paths.Node);
        if (!File.Exists(paths.EntryPoint))
            throw new FileNotFoundException("Playwright CLI is missing from the deployed bundle.", paths.EntryPoint);
    }

    /// <summary>
    /// عند فشل بدء المحرك فقط، يعيد تشغيل run-driver تشخيصيًا لجمع stderr ورمز الخروج.
    /// هذا تشغيل منفصل ولا يُنسب رمز خروجه إلى المحاولة الأصلية. لا يرسل أوامر جمع أو إطلاق متصفح.
    /// </summary>
    public static async Task<string> ProbeAsync(BrowserDriverPathsDto paths, CancellationToken ct = default)
    {
        using var process = new Process
        {
            StartInfo = new(paths.Node)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            }
        };
        process.StartInfo.Environment["PW_LANG_NAME"] = "csharp";
        process.StartInfo.Environment["PW_LANG_NAME_VERSION"] = $"{Environment.Version.Major}.{Environment.Version.Minor}";
        process.StartInfo.Environment["PW_CLI_DISPLAY_VERSION"] = typeof(Playwright).Assembly.GetName().Version!.ToString(3);
        process.StartInfo.ArgumentList.Add(paths.EntryPoint);
        process.StartInfo.ArgumentList.Add("run-driver");
        var started = false;
        Task<string>? output = null, error = null;
        try
        {
            ValidateBundle(paths);
            ct.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started) return "Diagnostic driver probe could not start.";
            output = DrainAsync(process.StandardOutput);
            error = DrainAsync(process.StandardError);
            try
            {
                await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(2), ct);
                return $"Diagnostic driver probe: ExitCode={process.ExitCode} (0x{unchecked((uint)process.ExitCode):X8}); "
                    + $"stderr={(await error).Trim()}; stdout={(await output).Trim()}";
            }
            catch (TimeoutException)
            {
                return "Diagnostic driver probe survived startup; original failure was not reproduced.";
            }
        }
        catch (Win32Exception ex)
        {
            return $"Diagnostic driver probe: Win32={ex.NativeErrorCode}; {ex.Message}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Diagnostic driver probe: {ex.GetType().Name}; {ex.Message}";
        }
        finally
        {
            if (started)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                    if (output is not null && error is not null)
                        await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or TimeoutException or IOException) { }
            }
        }
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var result = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
            if (result.Length < 4096) result.Append(buffer, 0, Math.Min(count, 4096 - result.Length));
        return result.ToString();
    }
}
