using HiringPs.Core;
using Microsoft.Extensions.Configuration;

internal static class ToolChecks
{
    public static async Task RunAsync()
    {
        await BrowserFallbackChecks.RunAsync();
        var count = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new Exception(label);
            count++;
            Console.WriteLine("PASS " + label);
        }
        foreach (
            var output in new[]
            {
                "tesseract v5.5.3.20260724\r\n leptonica-1.87.0",
                "tesseract 5.3.0\n leptonica-1.82.0",
            }
        )
        {
            var parsed = ToolProbes.ParseVersion("PdfOcr", output);
            Check(
                parsed.State == ToolState.Ready && parsed.Detail.StartsWith("5."),
                "Tesseract Windows and Linux version formats are recognized"
            );
        }
        Check(
            ToolProbes.ParseVersion("PdfOcr", "leptonica 1.87.0").State == ToolState.Problem,
            "a dependency version cannot be mistaken for Tesseract"
        );
        Check(
            ToolProbes.ParseVersion("PdfOcr", "pdftotext version 26.09.0").State
                == ToolState.Problem,
            "another executable cannot satisfy the OCR version check"
        );
        Check(
            PdfToolPaths.Executable("Tools/poppler/pdftotext.exe")
                == Path.Combine(AppContext.BaseDirectory, "Tools", "poppler", "pdftotext.exe"),
            "bundled executable resolves beside the application, independently of current directory"
        );
        Check(
            PdfToolPaths.Directory("tessdata")
                == Path.Combine(AppContext.BaseDirectory, "tessdata"),
            "relative OCR models resolve beside the application"
        );
        Check(
            PdfToolPaths.Executable("pdftotext") == "pdftotext",
            "bare executable names preserve PATH lookup"
        );
        var absoluteTool = Path.Combine(Path.GetTempPath(), "custom-tools", "pdftotext");
        Check(
            PdfToolPaths.Executable(absoluteTool) == absoluteTool,
            "absolute tool paths remain supported"
        );
        var browserConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Browser:BrowsersPath"] = ".playwright-browsers",
                    ["Browser:ExecutablePath"] = "Tools/chromium/chrome.exe",
                }
            )
            .Build();
        Check(
            BrowserSettings.BrowsersPath(browserConfig)
                == Path.Combine(AppContext.BaseDirectory, ".playwright-browsers"),
            "Chromium cache resolves from JSON beside the application"
        );
        Check(
            BrowserSettings.ExecutablePath(browserConfig)
                == Path.Combine(AppContext.BaseDirectory, "Tools", "chromium", "chrome.exe"),
            "custom Chromium executable also supports relative JSON paths"
        );
        var oldBrowserEnvironment = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "old-script-location");
            BrowserSettings.Configure(browserConfig);
            using var configuredDriver = await BrowserSettings.CreateAsync(browserConfig);
            Check(
                configuredDriver.Chromium.ExecutablePath.StartsWith(
                    BrowserSettings.BrowsersPath(browserConfig)! + Path.DirectorySeparatorChar
                ),
                "Playwright driver reads its Chromium location from the JSON-backed configuration"
            );
            Check(
                Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
                    == BrowserSettings.BrowsersPath(browserConfig),
                "JSON browser location takes precedence over the old startup script setting"
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", oldBrowserEnvironment);
        }
        var now = DateTimeOffset.UtcNow;
        var worker = new ToolWorkerState { Id = "current", At = now };
        var result = new ToolCheckResult
        {
            WorkerId = worker.Id,
            CheckedAt = now,
            State = ToolState.Ready,
        };
        Check(
            ToolMonitoringRules.WorkerFresh(worker, now),
            "current independent heartbeat is fresh"
        );
        Check(!ToolMonitoringRules.WorkerFresh(null, now), "absent worker is never healthy");
        Check(
            !ToolMonitoringRules.WorkerFresh(new() { At = now.AddMinutes(-2) }, now),
            "stopped worker heartbeat becomes stale"
        );
        Check(
            !ToolMonitoringRules.WorkerFresh(new() { At = now.AddMinutes(3) }, now),
            "future clock skew does not look healthy"
        );
        Check(
            ToolMonitoringRules.ResultFresh(result, worker, now),
            "current worker result remains usable"
        );
        Check(
            !ToolMonitoringRules.ResultFresh(result, new() { Id = "restarted", At = now }, now),
            "old worker instance result cannot show green after restart"
        );
        result.CheckedAt = now.AddHours(-27);
        Check(
            !ToolMonitoringRules.ResultFresh(result, worker, now),
            "old check expires despite a live heartbeat"
        );
        result.CheckedAt = now.AddHours(-23);
        Check(
            ToolMonitoringRules.ResultFresh(result, worker, now),
            "daily standard result stays usable until the next daily check"
        );
        result.Tool = "AiLive";
        result.CheckedAt = now.AddHours(-3);
        Check(
            !ToolMonitoringRules.ResultFresh(result, worker, now),
            "paid AI connectivity result keeps its shorter freshness window"
        );
        foreach (
            var instant in new[]
            {
                new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero),
            }
        )
        {
            var due = ToolCheckSchedule.NextMidnight(instant);
            var localDue = TimeZoneInfo.ConvertTime(due, SourceSchedule.Zone);
            Check(
                due > instant
                    && localDue.TimeOfDay == TimeSpan.Zero
                    && SourceSchedule.LocalDate(due)
                        == SourceSchedule.LocalDate(instant).AddDays(1),
                "daily check follows Palestine midnight in winter, summer and transition season"
            );
            Check(
                ToolCheckSchedule.NextMidnight(due) > due,
                "a handled midnight schedules the following day instead of repeating"
            );
        }
        var runtime = new ToolWorkerRuntime();
        runtime.Begin();
        Check(
            runtime.Snapshot.Collecting && runtime.Snapshot.LastLoopAt is not null,
            "collection start is visible independently from heartbeat"
        );
        runtime.Fail(new IOException("private detail must not be recorded"));
        Check(
            !runtime.Snapshot.Collecting && runtime.Snapshot.ErrorType == "IOException",
            "collection failure records only the exception type"
        );
        runtime.Begin();
        runtime.Succeed();
        Check(
            runtime.Snapshot.LastSuccessAt >= runtime.Snapshot.LastFailureAt
                && runtime.Snapshot.ErrorType == "",
            "a later successful loop clears the active failure"
        );
        var settings = new Dictionary<string, string?>();
        foreach (
            var name in new[]
            {
                "Pdf__TextExecutable",
                "Pdf__RenderExecutable",
                "Pdf__OcrExecutable",
                "Pdf__OcrDataPath",
                "Browser__ExecutablePath",
            }
        )
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
                settings[name.Replace("__", ":")] = value;
        var folder = Path.Combine(
            Path.GetTempPath(),
            "hiring-tools-" + Guid.NewGuid().ToString("N")
        );
        settings["Pdf:WorkDirectory"] = Path.Combine(folder, "archive");
        IConfiguration Config(Dictionary<string, string?>? more = null) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .AddInMemoryCollection(more ?? [])
                .Build();
        try
        {
            var probes = new ToolProbes(Config());
            foreach (
                var key in new[]
                {
                    "PdfText",
                    "PdfRender",
                    "PdfOcr",
                    "PdfLanguages",
                    "PdfWorkspace",
                    "PdfPipeline",
                    "Chromium",
                }
            )
            {
                var probe = await probes.CheckAsync(key, default);
                Check(
                    probe.State == ToolState.Ready,
                    "real worker-environment probe: " + key + " / " + probe.Code
                );
                if (key == "Chromium")
                    Check(probe.Detail.Contains("Version ") &&
                        (probe.Detail.StartsWith("Microsoft Edge;") || probe.Detail.StartsWith("Chromium;") || probe.Detail.StartsWith("Custom executable;")),
                        "browser probe reports its actual launch selection and version");
            }
            Check(
                !Directory.EnumerateFiles(settings["Pdf:WorkDirectory"]!).Any(),
                "temporary workspace probe removes its temporary file"
            );
            var missingKey = await probes.CheckAsync("AiConfiguration", default);
            Check(
                missingKey.State == ToolState.NotConfigured,
                "AI key absence is checked without provider traffic"
            );
            var configured = await new ToolProbes(
                Config(new() { ["Ai:ApiKey"] = "fixture-only", ["Ai:Model"] = "gemini-fixture" })
            ).CheckAsync("AiConfiguration", default);
            Check(
                configured.State == ToolState.Configured && configured.Detail == "gemini-fixture",
                "AI configuration presence does not claim verified connectivity"
            );
            var malformed = await new ToolProbes(
                Config(new() { ["Ai:ApiKey"] = "fixture-secret", ["Ai:Model"] = "fixture-secret" })
            ).CheckAsync("AiConfiguration", default);
            Check(
                malformed.State == ToolState.NotConfigured
                    && !malformed.Detail.Contains("fixture-secret"),
                "misplaced credentials are never reflected as model details"
            );
            foreach (
                var pair in new[]
                {
                    (Key: "", Model: "gemini-fixture"),
                    (Key: "fixture-only", Model: ""),
                    (Key: "fixture-only", Model: "bad/model"),
                }
            )
            {
                using var handler = new NoNetworkHandler();
                using var client = new HttpClient(handler);
                var blocked = false;
                try
                {
                    await new AiExtractor(
                        client,
                        Config(new() { ["Ai:ApiKey"] = pair.Key, ["Ai:Model"] = pair.Model })
                    ).ExtractAsync("probe", default);
                }
                catch (AiUnavailableException)
                {
                    blocked = true;
                }
                Check(
                    blocked && handler.Calls == 0,
                    "missing/invalid AI configuration remains queued without provider traffic: "
                        + (
                            pair.Key.Length == 0 ? "key"
                            : pair.Model.Length == 0 ? "model"
                            : "invalid model"
                        )
                );
            }
            var missing = await new ToolProbes(
                Config(new() { ["Pdf:TextExecutable"] = Path.Combine(folder, "missing") })
            ).CheckAsync("PdfText", default);
            Check(
                missing.State == ToolState.Problem && missing.Code == "ToolMissing",
                "missing executable returns an actionable failure"
            );
            Directory.CreateDirectory(Path.Combine(folder, "no-languages"));
            var language = await new ToolProbes(
                Config(new() { ["Pdf:OcrDataPath"] = Path.Combine(folder, "no-languages") })
            ).CheckAsync("PdfLanguages", default);
            Check(
                language.State == ToolState.Problem && language.Code == "ToolExecutionFailed",
                "unreadable traineddata returns a problem before invoking OCR"
            );
            var disabled = await new ToolProbes(
                Config(new() { ["Pdf:EnableOcr"] = "false" })
            ).CheckAsync("PdfLanguages", default);
            Check(
                disabled.State == ToolState.Disabled,
                "disabled OCR is distinct from installed language data"
            );
            var archive = await new ToolProbes(
                Config(new() { ["Pdf:WorkDirectory"] = Path.Combine(AppContext.BaseDirectory, "wwwroot", "archive") })
            ).CheckAsync("PdfWorkspace", default);
            Check(
                archive.State == ToolState.Problem,
                "unsafe temporary directory configuration is surfaced as a problem"
            );
            var badBrowser = await new ToolProbes(
                Config(
                    new() { ["Browser:ExecutablePath"] = Path.Combine(folder, "missing-browser") }
                )
            ).CheckAsync("Chromium", default);
            Check(
                badBrowser.State == ToolState.Problem,
                "browser execution failures cannot appear healthy"
            );
            Console.WriteLine(
                $"{count} tool checks passed with real local browser/PDF probes; no SQL or AI provider requests."
            );
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            throw new InvalidOperationException(
                "Unexpected provider call in a configuration-only check."
            );
        }
    }
}
