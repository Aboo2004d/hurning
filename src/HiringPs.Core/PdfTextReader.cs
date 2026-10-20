using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HiringPs.Core;

// Arabic is transcribed from the rendered page: reversing strings can corrupt dates and English names.
public sealed class PdfTextReader(IConfiguration configuration)
{
    private readonly PdfTools tools = new(configuration);

    public async Task CheckSetupAsync(CancellationToken ct)
    {
        await tools.RunAsync("TextExecutable", "pdftotext", ["-v"], ct);
        if (OcrEnabled)
        {
            await tools.RunAsync("RenderExecutable", "pdftoppm", ["-v"], ct);
            await CheckLanguagesAsync(ct);
        }
        PdfTextFiles.Prepare(configuration);
        var root = PdfTextFiles.DirectoryPath(configuration);
        var test = Path.Combine(root, ".probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllTextAsync(test, "PDF setup check", ct);
        }
        finally
        {
            if (File.Exists(test))
                File.Delete(test);
        }
    }

    private bool OcrEnabled =>
        !string.Equals(configuration["Pdf:EnableOcr"], "false", StringComparison.OrdinalIgnoreCase);

    private async Task CheckLanguagesAsync(CancellationToken ct)
    {
        var arguments = DataArguments();
        arguments.Add("--list-langs");
        var output = await tools.RunAsync("OcrExecutable", "tesseract", arguments, ct);
        var languages = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.Ordinal);
        if (!languages.Contains("ara") || !languages.Contains("eng"))
            throw new ContentNeedsReviewException(
                "PDF OCR needs both ara and eng traineddata; configure Pdf:OcrDataPath and verify tesseract --list-langs under the worker account."
            );
    }

    private List<string> DataArguments() =>
        configuration["Pdf:OcrDataPath"] is { Length: > 0 } path
            ? ["--tessdata-dir", PdfToolPaths.Directory(path)]
            : [];

    public async Task<PdfTextResult> ReadAsync(byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length > AttachmentReader.MaxBytes)
            throw new ContentNeedsReviewException("Attachment exceeds 8 MB limit.");
        if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
            throw new ContentNeedsReviewException("Attachment is not a readable PDF.");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromMinutes(3));
        PdfTextFiles.Prepare(configuration);
        var work = Path.Combine(
            PdfTextFiles.DirectoryPath(configuration),
            Guid.NewGuid().ToString("N")
        );
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var pages = new List<PdfPageText>();
        var readable = new StringBuilder(
            $"[PDF DOCUMENT {hash}; TEXT VERSION {PdfTextFiles.TextVersion}]\n"
        );
        try
        {
            if (OperatingSystem.IsWindows())
                Directory.CreateDirectory(work);
            else
                Directory.CreateDirectory(
                    work,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                );
            using var lease = new FileStream(
                Path.Combine(work, ".active"),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None
            );
            var input = Path.Combine(work, "source.pdf");
            await File.WriteAllBytesAsync(input, bytes, budget.Token);
            using var document = Open(bytes);
            if (document.NumberOfPages is < 1 or > 40)
                throw new ContentNeedsReviewException(
                    "PDF exceeds 40 pages or has no pages; review needed."
                );
            // Run Poppler once for the whole document, retaining form-feed page boundaries.
            var nativeFile = Path.Combine(work, "native.txt");
            await tools.RunAsync(
                "TextExecutable",
                "pdftotext",
                ["-enc", "UTF-8", "-layout", "-eol", "unix", input, nativeFile],
                budget.Token
            );
            var native = (await ReadBoundedAsync(nativeFile, budget.Token)).Split('\f');
            var checkedLanguages = false;
            foreach (var page in document.GetPages())
            {
                budget.Token.ThrowIfCancellationRequested();
                var raw = ContentOrderTextExtractor.GetText(page);
                var text = Clean(page.Number <= native.Length ? native[page.Number - 1] : "");
                var method = "poppler";
                double? confidence = null;
                var blank =
                    page.Letters.Count == 0
                    && page.Paths.Count == 0
                    && page.NumberOfImages == 0
                    && text.Trim().Length == 0;
                // Native Arabic may look plausible while its glyph order is reversed. OCR uses visual order.
                if (
                    !blank
                    && (
                        page.NumberOfImages > 0
                        || HasArabic(text)
                        || HasArabic(raw)
                        || !Readable(text)
                    )
                )
                {
                    if (!OcrEnabled)
                        throw new ContentNeedsReviewException(
                            $"PDF page {page.Number} needs Arabic/image OCR, but Pdf:EnableOcr is false."
                        );
                    if (!checkedLanguages)
                    {
                        await CheckLanguagesAsync(budget.Token);
                        checkedLanguages = true;
                    }
                    var image = Path.Combine(work, "page");
                    var number = page.Number.ToString(CultureInfo.InvariantCulture);
                    await tools.RunAsync(
                        "RenderExecutable",
                        "pdftoppm",
                        [
                            "-f",
                            number,
                            "-l",
                            number,
                            "-singlefile",
                            "-scale-to",
                            "2500",
                            "-gray",
                            "-png",
                            input,
                            image,
                        ],
                        budget.Token
                    );
                    var output = Path.Combine(work, "ocr");
                    var args = new List<string> { image + ".png", output };
                    args.AddRange(DataArguments());
                    args.AddRange([
                        "-l",
                        "ara+eng",
                        "--psm",
                        "3",
                        "-c",
                        "tessedit_create_txt=1",
                        "-c",
                        "tessedit_create_tsv=1",
                    ]);
                    await tools.RunAsync("OcrExecutable", "tesseract", args, budget.Token);
                    text = Clean(await ReadBoundedAsync(output + ".txt", budget.Token));
                    confidence = Confidence(await ReadBoundedAsync(output + ".tsv", budget.Token));
                    method = "ocr:ara+eng";
                    if (
                        !Readable(text)
                        || confidence is null
                        || confidence < 60
                        || (HasArabic(raw) && !HasArabic(text))
                    )
                    {
                        pages.Add(new(page.Number, method, text, confidence));
                        readable.AppendLine($"[PDF PAGE {page.Number}]").AppendLine(text);
                        throw new ContentNeedsReviewException(
                            $"PDF page {page.Number} OCR is unreadable or low-confidence; compare the original at its source before publishing."
                        );
                    }
                    File.Delete(image + ".png");
                }
                if (blank)
                    method = "blank";
                pages.Add(new(page.Number, method, text, confidence));
                readable.AppendLine($"[PDF PAGE {page.Number}]").AppendLine(text);
                EnsureBounded(readable.ToString());
            }
            if (
                pages.Any(x => x.Method.StartsWith("ocr:", StringComparison.Ordinal))
                && !string.Equals(
                    configuration["Pdf:RequireOcrReview"],
                    "false",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                readable.AppendLine(PdfTextFiles.ReviewMarker);
            readable.AppendLine("[END PDF]");
            EnsureBounded(readable.ToString());
            if (pages.Sum(x => x.Text.Count(char.IsLetter)) < 40)
                throw new ContentNeedsReviewException(
                    "PDF has insufficient readable content; manual review required."
                );
            return new(hash, readable.ToString(), pages);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Often caused by a busy server; repeated failures reach review through the attempt limit.
            throw new ContentRetryableException(
                $"PDF document {hash}: processing exceeded the 3 minute budget; retry scheduled."
            );
        }
        catch (ContentNeedsReviewException ex)
        {
            throw new ContentNeedsReviewException($"PDF document {hash}: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ContentNeedsReviewException(
                $"PDF document {hash}: processing failed; check the document, tools and temporary-directory permissions."
            );
        }
        finally
        {
            // Tools have exited and document/lock handles are disposed before deleting every intermediate file.
            await PdfTextFiles.DeleteWorkAsync(work);
        }
    }

    private static PdfDocument Open(byte[] bytes)
    {
        try
        {
            return PdfDocument.Open(bytes);
        }
        catch (Exception)
        {
            throw new ContentNeedsReviewException(
                "PDF is damaged, encrypted or unsupported; manual review required."
            );
        }
    }

    private static string Clean(string text) =>
        Regex
            .Replace(
                text.Normalize(NormalizationForm.FormKC),
                @"[\u061C\u200E\u200F\u202A-\u202E\u2066-\u2069]",
                ""
            )
            .Trim();

    private static bool HasArabic(string text) =>
        Regex.IsMatch(text, @"[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]");

    private static bool Readable(string text) =>
        text.Count(char.IsLetter) >= 5
        && !text.Contains('\uFFFD')
        && text.Count(char.IsControl) <= text.Count(x => x is '\n' or '\r' or '\t');

    private static void EnsureBounded(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > PdfTextFiles.MaxTextBytes)
            throw new ContentNeedsReviewException("PDF extracted text exceeds the 2 MB limit.");
    }

    private static async Task<string> ReadBoundedAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > PdfTextFiles.MaxTextBytes)
            throw new ContentNeedsReviewException(
                "PDF tool output is missing or exceeds the 2 MB limit."
            );
        return await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
    }

    private static double? Confidence(string tsv)
    {
        var words = tsv.Split('\n')
            .Skip(1)
            .Select(line => line.TrimEnd('\r').Split('\t'))
            .Where(x => x.Length >= 12 && x[0] == "5" && x[11].Any(char.IsLetterOrDigit));
        var counts = words
            .Select(x =>
                double.TryParse(x[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var c)
                && c >= 0
                    ? (double?)c
                    : null
            )
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();
        return counts.Length == 0 ? null : Math.Round(counts.Average(), 2);
    }
}
