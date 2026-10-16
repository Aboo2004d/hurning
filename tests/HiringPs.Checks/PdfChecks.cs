using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.Extensions.Configuration;

internal static class PdfChecks
{
    public static async Task ProcessFixtureAsync(string[] args)
    {
        var index = Array.IndexOf(args, "--pdf-process-fixture");
        await File.WriteAllTextAsync(args[index + 1], Environment.ProcessId.ToString());
        await Task.Delay(TimeSpan.FromMinutes(2));
    }

    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception(label);
            passed++;
            Console.WriteLine("PASS " + label);
        }
        async Task Reject(Func<Task> action, string expected, string label)
        {
            try
            {
                await action();
                throw new Exception("Expected failure: " + label);
            }
            catch (ContentNeedsReviewException ex)
            {
                Check(ex.Message.Contains(expected, StringComparison.OrdinalIgnoreCase), label);
            }
        }
        var folder = Path.Combine(
            Path.GetTempPath(),
            "hiring-pdf-checks-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(folder);
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pdf");
        var settings = new Dictionary<string, string?>
        {
            ["Pdf:TextArchivePath"] = Path.Combine(folder, "archive"),
        };
        foreach (
            var name in new[]
            {
                "TextExecutable",
                "RenderExecutable",
                "OcrExecutable",
                "OcrDataPath",
            }
        )
            if (Environment.GetEnvironmentVariable("Pdf__" + name) is { Length: > 0 } value)
                settings["Pdf:" + name] = value;
        IConfiguration Config(Dictionary<string, string?>? overrides = null) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .AddInMemoryCollection(overrides ?? [])
                .Build();
        Task<PdfTextResult> Read(
            string name,
            IConfiguration? config = null,
            CancellationToken ct = default
        ) =>
            new PdfTextReader(config ?? Config()).ReadAsync(
                File.ReadAllBytes(Path.Combine(fixture, name)),
                ct
            );
        try
        {
            await new PdfTextReader(Config()).CheckSetupAsync(CancellationToken.None);
            Check(
                true,
                "real Poppler, Tesseract, Arabic/English language data and archive write access"
            );
            var english = await Read("english-native.pdf");
            Check(
                english.Text.Contains("Project Management Specialist")
                    && english.Pages.Single().Method == "poppler",
                "English native PDF preserves text and uses the native path"
            );
            Check(
                !english.Text.Contains(PdfTextFiles.ReviewMarker),
                "native text does not require OCR review"
            );
            var arabic = await Read("arabic-native.pdf");
            Check(
                TextEvidence.Contains(arabic.Text, "إعلان عطاء لتوريد أجهزة حاسوب")
                    && TextEvidence.Contains(arabic.Text, "رام الله"),
                "Arabic native PDF is read in visual order without reversing digits or English"
            );
            Check(
                OpportunityService.DateEvidenceMatches(arabic.Text, new(2026, 8, 16)),
                "Arabic PDF retains the numeric deadline"
            );
            Check(
                arabic.Pages.Single().Method == "ocr:ara+eng"
                    && arabic.Text.Contains(PdfTextFiles.ReviewMarker),
                "Arabic OCR includes a visual review requirement by default"
            );
            var scan = await Read("arabic-scanned.pdf");
            Check(
                TextEvidence.Contains(scan.Text, "إعلان عطاء لتوريد أجهزة حاسوب")
                    && scan.Text.Contains("TEST-PDF-123"),
                "image-only bilingual PDF is transcribed with Arabic and English"
            );
            Check(
                scan.Pages.Single().Confidence >= 60,
                "OCR confidence is measured from actual Tesseract TSV output"
            );
            var mixed = await Read("mixed-pages.pdf");
            Check(
                mixed.Pages.Count == 2
                    && mixed.Pages[0].Method == "poppler"
                    && mixed.Pages[1].Method == "ocr:ara+eng",
                "mixed native/scanned document is processed page by page"
            );
            var evidence = "Closing date: 16/08/2026";
            Check(
                PdfTextFiles.FindPage(scan.Text, evidence) == new PdfPageReference(scan.Hash, 1),
                "evidence page is determined from stored source text"
            );
            Check(
                PdfTextFiles.FindPage(mixed.Text, "إعلان عطاء لتوريد أجهزة حاسوب")?.Page == 2,
                "mixed document evidence points to the correct second page"
            );
            Check(
                PdfTextFiles.FindPage(scan.Text, "invented evidence") is null,
                "an unsupported quotation receives no page number"
            );
            foreach (var format in new[] { "source", "original", "readable", "report" })
                Check(
                    File.Exists(PdfTextFiles.FilePath(Config(), scan.Hash, format)),
                    "private archive exists: " + format
                );
            Check(
                File.ReadAllBytes(PdfTextFiles.FilePath(Config(), scan.Hash, "source"))
                    .SequenceEqual(File.ReadAllBytes(Path.Combine(fixture, "arabic-scanned.pdf"))),
                "archive preserves the original source PDF bytes"
            );
            using (
                var json = JsonDocument.Parse(
                    await File.ReadAllTextAsync(
                        PdfTextFiles.FilePath(Config(), scan.Hash, "report")
                    )
                )
            )
                Check(
                    json.RootElement.GetProperty("Complete").GetBoolean()
                        && json.RootElement.GetProperty("RequiresVisualReview").GetBoolean(),
                    "report separates completed transcription from visual review"
                );
            var automatic = await Read(
                "arabic-scanned.pdf",
                Config(new() { ["Pdf:RequireOcrReview"] = "false" })
            );
            Check(
                !automatic.Text.Contains(PdfTextFiles.ReviewMarker),
                "OCR review policy can be deliberately changed without changing OCR output"
            );
            await Reject(
                () => Read("vector-unreadable.pdf"),
                "low-confidence",
                "vector-only graphics are not silently classified as a blank page"
            );
            var columns = await Read("columns.pdf");
            Check(
                columns.Text.Contains("LEFT-123") && columns.Text.Contains("RIGHT-456"),
                "native columns retain both opportunity references"
            );
            await Reject(
                () => Read("image-unreadable.pdf"),
                "low-confidence",
                "unreadable image page is rejected instead of silently omitted"
            );
            await Reject(
                () =>
                    new PdfTextReader(Config()).ReadAsync(
                        Encoding.ASCII.GetBytes("%PDF-1.7\nbroken"),
                        default
                    ),
                "damaged",
                "damaged PDF is rejected clearly"
            );
            await Reject(
                () => Read("blank.pdf"),
                "insufficient",
                "blank PDF cannot become a published transcription"
            );
            await Reject(
                () => Read("encrypted.pdf"),
                "encrypted",
                "password protected PDF requires review"
            );
            await Reject(
                () => Read("too-many-pages.pdf"),
                "40 pages",
                "document page limit is enforced"
            );
            await Reject(
                () =>
                    new PdfTextReader(Config()).ReadAsync(
                        Encoding.UTF8.GetBytes("<html>blocked</html>"),
                        default
                    ),
                "not a readable PDF",
                "HTML protection response is not accepted as PDF"
            );
            await Reject(
                () =>
                    new PdfTextReader(Config()).ReadAsync(
                        new byte[AttachmentReader.MaxBytes + 1],
                        default
                    ),
                "8 MB",
                "document byte limit is enforced before processing"
            );
            await Reject(
                () => Read("arabic-scanned.pdf", Config(new() { ["Pdf:EnableOcr"] = "false" })),
                "false",
                "disabled OCR does not silently omit scanned pages"
            );
            await Reject(
                () =>
                    Read(
                        "english-native.pdf",
                        Config(
                            new() { ["Pdf:TextExecutable"] = Path.Combine(folder, "missing-tool") }
                        )
                    ),
                "unavailable",
                "missing native tool produces an actionable review error"
            );
            var emptyData = Path.Combine(folder, "empty-tessdata");
            Directory.CreateDirectory(emptyData);
            await Reject(
                () => Read("arabic-scanned.pdf", Config(new() { ["Pdf:OcrDataPath"] = emptyData })),
                "ara and eng",
                "missing Arabic language data is detected"
            );
            using (
                var report = JsonDocument.Parse(
                    await File.ReadAllTextAsync(
                        PdfTextFiles.FilePath(Config(), scan.Hash, "report")
                    )
                )
            )
                Check(
                    !report.RootElement.GetProperty("Complete").GetBoolean(),
                    "failed processing leaves a diagnostic report without returning partial content"
                );
            await Reject(
                () =>
                    Read(
                        "english-native.pdf",
                        Config(
                            new()
                            {
                                ["Pdf:TextArchivePath"] = Path.Combine(folder, "wwwroot", "pdf"),
                            }
                        )
                    ),
                "outside wwwroot",
                "archives cannot be exposed under wwwroot"
            );
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try
                {
                    await Read("english-native.pdf", ct: cancelled.Token);
                    throw new Exception("Cancellation was ignored");
                }
                catch (OperationCanceledException)
                {
                    Check(true, "document cancellation propagates without a successful result");
                }
            }
            Check(
                PdfRetry.NeedsFreshContent(new() { Url = "https://example.com/file.pdf?x=1" }),
                "retry refreshes PDF URLs even with a query string"
            );
            Check(
                PdfRetry.NeedsFreshContent(
                    new()
                    {
                        Url = "https://example.com/item",
                        AttachmentsJson = "[\"https://example.com/a.pdf\"]",
                    }
                ),
                "retry refreshes announcements with PDF attachments"
            );
            Check(
                !PdfRetry.NeedsFreshContent(
                    new()
                    {
                        Url = "https://example.com/item",
                        Content = "normal HTML text",
                        AttachmentsJson = "[]",
                    }
                ),
                "ordinary HTML retry keeps its saved content path"
            );
            Check(
                DateEvidenceRules
                    .Check(
                        new()
                        {
                            Deadline = new(2026, 8, 16),
                            DeadlineEvidence = "آخر موعد للتقديم١٦/٠٨/٢٠٢٦",
                        },
                        "آخر موعد للتقديم١٦/٠٨/٢٠٢٦"
                    )
                    .Errors.Count == 0,
                "Arabic digits and dates adjacent to source labels are verified"
            );
            var pidFile = Path.Combine(folder, "child.pid");
            var host = Environment.ProcessPath!;
            var childArguments = new List<string>();
            if (
                Path.GetFileNameWithoutExtension(host)
                    .Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            )
                childArguments.Add(Assembly.GetExecutingAssembly().Location);
            childArguments.AddRange(["--pdf-process-fixture", pidFile]);
            await Reject(
                () =>
                    new PdfTools(
                        Config(
                            new()
                            {
                                ["Pdf:TextExecutable"] = host,
                                ["Pdf:ProcessTimeoutSeconds"] = "2",
                            }
                        )
                    ).RunAsync("TextExecutable", "fixture", childArguments, default),
                "timed out",
                "external process deadline is enforced"
            );
            Check(File.Exists(pidFile), "timeout test actually started its child process");
            var pid = int.Parse(await File.ReadAllTextAsync(pidFile));
            try
            {
                using var process = Process.GetProcessById(pid);
                Check(process.HasExited, "timed out process is terminated");
            }
            catch (ArgumentException)
            {
                Check(true, "timed out process is terminated");
            }
            Console.WriteLine(
                $"{passed} PDF checks passed with real local extraction/OCR; no SQL, AI calls or publication."
            );
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
