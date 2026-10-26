using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يشغل فحوص جاهزية الأدوات دون إنشاء فرص أو إجراء دفعات أو نشر محتوى خارجي.</summary>
public sealed class ToolProbes(IConfiguration configuration)
{
    /// <summary>يشغل فحص الأداة المطلوبة ضمن مهلة ويحول فشلها إلى نتيجة تشخيصية آمنة.</summary>
    public async Task<ToolProbe> CheckAsync(string tool, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            return tool switch
            {
                "AiConfiguration" => AiConfiguration(),
                "Chromium" => await ChromiumAsync(),
                "PdfText" => await VersionAsync(
                    tool,
                    "TextExecutable",
                    "pdftotext",
                    "-v",
                    budget.Token
                ),
                "PdfRender" => await VersionAsync(
                    tool,
                    "RenderExecutable",
                    "pdftoppm",
                    "-v",
                    budget.Token
                ),
                "PdfOcr" => await VersionAsync(
                    tool,
                    "OcrExecutable",
                    "tesseract",
                    "--version",
                    budget.Token
                ),
                "PdfLanguages" => await LanguagesAsync(budget.Token),
                "PdfWorkspace" => await WorkspaceAsync(budget.Token),
                "PdfPipeline" => await PdfPipelineAsync(budget.Token),
                _ => throw new ArgumentException("Unknown diagnostic tool."),
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(tool, ToolState.Problem, "CheckTimeout");
        }
        catch (PdfToolFailureException ex)
        {
            return new(tool, ToolState.Problem, "ToolExecutionFailed", ex.Message.Truncate(1000));
        }
        catch (ContentNeedsReviewException ex)
        {
            return new(
                tool,
                ToolState.Problem,
                ex.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
                    ? "ToolMissing"
                    : "ToolExecutionFailed",
                Detail(ex)
            );
        }
        catch (PlaywrightException ex)
        {
            var code = ex.Message.Contains(
                "Executable doesn't exist",
                StringComparison.OrdinalIgnoreCase
            )
                ? "BrowserMissing"
                : "BrowserLaunchFailed";
            return new(tool, ToolState.Problem, code, Detail(ex));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(tool, ToolState.Problem, "AccessFailed", Detail(ex));
        }
    }

    // The first lines of the failure (exception type, message, Playwright's browser log)
    // are shown to administrators so a server problem can be diagnosed without file access.
    private static string Detail(Exception ex)
    {
        var lines = ex.Message.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        return (ex.GetType().Name + ": " + string.Join(" | ", lines.Take(12))).Truncate(1000);
    }

    /// <summary>يتحقق من وجود مفتاح AI وصيغة النموذج دون إرسال طلب مدفوع.</summary>
    private ToolProbe AiConfiguration()
    {
        var key = configuration["Ai:ApiKey"];
        var model = configuration["Ai:Model"] ?? "";
        if (string.IsNullOrWhiteSpace(key) || key == "***")
            return new("AiConfiguration", ToolState.NotConfigured, "AiKeyMissing");
        if (model == key || !Regex.IsMatch(model, @"\Agemini-[a-zA-Z0-9._-]{1,70}\z"))
            return new("AiConfiguration", ToolState.NotConfigured, "AiModelInvalid");
        return new("AiConfiguration", ToolState.Configured, "AiConfigured", model);
    }

    /// <summary>يشغّل Chromium ويفحص تنفيذ JavaScript محليًا دون جمع مصدر خارجي.</summary>
    private async Task<ToolProbe> ChromiumAsync()
    {
        using var runtime = await BrowserSettings.CreateAsync(configuration);
        await using var browser = await runtime.Chromium.LaunchAsync(
            new()
            {
                Headless = true,
                ExecutablePath = BrowserSettings.ExecutablePath(configuration),
                Timeout = 20000,
                Channel = configuration["Browser:Channel"] is { Length: > 0 } channel
                    ? channel
                    : null,
                Args = BrowserSettings.Args(configuration),
            }
        );
        var page = await browser.NewPageAsync();
        page.SetDefaultTimeout(5000);
        await page.SetContentAsync("<!doctype html><title>Tool diagnostic</title>");
        var correct = await page.EvaluateAsync<int>("() => 21 * 2") == 42;
        return new(
            "Chromium",
            correct ? ToolState.Ready : ToolState.Problem,
            correct ? "BrowserReady" : "BrowserLaunchFailed",
            correct ? browser.Version.Truncate(80) : ""
        );
    }

    /// <summary>يشغّل أداة PDF لقراءة إصدارها بدل الاكتفاء بوجود الملف.</summary>
    private async Task<ToolProbe> VersionAsync(
        string tool,
        string setting,
        string executable,
        string argument,
        CancellationToken ct
    )
    {
        var output = await new PdfTools(configuration).RunAsync(
            setting,
            executable,
            [argument],
            ct
        );
        return ParseVersion(tool, output);
    }

    /// <summary>يستخرج رقم الإصدار من خرج الأداة دون اعتماد خرج غير معروف كنجاح.</summary>
    public static ToolProbe ParseVersion(string tool, string output)
    {
        var executable = tool switch
        {
            "PdfText" => "pdftotext",
            "PdfRender" => "pdftoppm",
            "PdfOcr" => "tesseract",
            _ => throw new ArgumentException("Unknown version diagnostic tool.", nameof(tool)),
        };
        var version = Regex.Match(
            output,
            @"(?im)^\s*" + executable + @"\s+(?:version\s+)?v?(?<v>\d[\w.\-]{0,60})(?=\s|$)"
        );
        return version.Success
            ? new(tool, ToolState.Ready, "ToolReady", version.Groups["v"].Value)
            : new(tool, ToolState.Problem, "VersionUnknown");
    }

    /// <summary>يفحص قائمة لغات Tesseract ويتأكد من توفر العربية والإنجليزية.</summary>
    private async Task<ToolProbe> LanguagesAsync(CancellationToken ct)
    {
        if (
            string.Equals(
                configuration["Pdf:EnableOcr"],
                "false",
                StringComparison.OrdinalIgnoreCase
            )
        )
            return new("PdfLanguages", ToolState.Disabled, "OcrDisabled");
        var args = new List<string>();
        if (configuration["Pdf:OcrDataPath"] is { Length: > 0 } path)
            args.AddRange(["--tessdata-dir", PdfToolPaths.Directory(path)]);
        args.Add("--list-langs");
        var output = await new PdfTools(configuration).RunAsync(
            "OcrExecutable",
            "tesseract",
            args,
            ct
        );
        var languages = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.Ordinal);
        return languages.Contains("ara") && languages.Contains("eng")
            ? new("PdfLanguages", ToolState.Ready, "LanguagesReady", "ara + eng")
            : new(
                "PdfLanguages",
                ToolState.NotConfigured,
                "LanguagesMissing",
                $"ara: {languages.Contains("ara")}; eng: {languages.Contains("eng")}"
            );
    }

    /// <summary>يفحص سلسلة PDF بنص أصلي وصفحة OCR ويقارن النتائج المتوقعة.</summary>
    private async Task<ToolProbe> PdfPipelineAsync(CancellationToken ct)
    {
        if (
            string.Equals(
                configuration["Pdf:EnableOcr"],
                "false",
                StringComparison.OrdinalIgnoreCase
            )
        )
            return new("PdfPipeline", ToolState.Disabled, "OcrDisabled");
        var folder = Path.Combine(
            Path.GetTempPath(),
            "hiring-tools-pdf-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            await using var asset =
                typeof(ToolProbes).Assembly.GetManifestResourceStream(
                    "HiringPs.Core.Diagnostics.PdfProbe.pdf"
                ) ?? throw new InvalidOperationException("Missing PDF probe fixture.");
            using var memory = new MemoryStream();
            await asset.CopyToAsync(memory, ct);
            var config = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Pdf:WorkDirectory"] = Path.Combine(folder, "archive"),
                    }
                )
                .Build();
            var result = await new PdfTextReader(config).ReadAsync(memory.ToArray(), ct);
            var valid =
                result.Pages.Count == 2
                && result.Pages[0].Method == "poppler"
                && result.Pages[1].Method == "ocr:ara+eng"
                && TextEvidence.Contains(result.Pages[1].Text, "رام الله")
                && result.Text.Contains("TEST-PDF-123", StringComparison.Ordinal);
            return new(
                "PdfPipeline",
                valid ? ToolState.Ready : ToolState.Problem,
                valid ? "PdfPipelineReady" : "ToolExecutionFailed"
            );
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>يفحص إنشاء ملف مؤقت وحذفه بصلاحيات حساب العامل.</summary>
    private async Task<ToolProbe> WorkspaceAsync(CancellationToken ct)
    {
        var root = PdfTextFiles.DirectoryPath(configuration);
        PdfTextFiles.Prepare(configuration);
        var path = Path.Combine(root, ".tools-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllTextAsync(path, "Tool temporary workspace probe", ct);
            if (await File.ReadAllTextAsync(path, ct) != "Tool temporary workspace probe")
                return new("PdfWorkspace", ToolState.Problem, "AccessFailed");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        return new("PdfWorkspace", ToolState.Ready, "WorkspaceReady");
    }
}
