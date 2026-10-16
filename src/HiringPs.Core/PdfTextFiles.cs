using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

public record PdfPageText(int Page, string Method, string Text, double? Confidence = null);

public record PdfTextResult(string Hash, string Text, IReadOnlyList<PdfPageText> Pages);

public record PdfPageReference(string Hash, int Page);

public static class PdfTextFiles
{
    public const int TextVersion = 1;
    public const string ReviewMarker = "[PDF OCR REVIEW REQUIRED]";
    public const int MaxTextBytes = 2 * 1024 * 1024;

    public static string DirectoryPath(IConfiguration configuration)
    {
        var configured = configuration["Pdf:TextArchivePath"] ?? Path.Combine("logs", "pdf-text");
        var path = Path.GetFullPath(configured, AppContext.BaseDirectory);
        if (
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(x => x.Equals("wwwroot", StringComparison.OrdinalIgnoreCase))
        )
            throw new ContentNeedsReviewException("PDF archive must be outside wwwroot.");
        return path;
    }

    public static string FilePath(IConfiguration configuration, string hash, string format)
    {
        if (!Regex.IsMatch(hash, @"\A[A-Fa-f0-9]{64}\z"))
            throw new ArgumentException("Invalid PDF hash.");
        var suffix = format switch
        {
            "source" => ".pdf",
            "original" => ".original.txt",
            "readable" => ".readable.txt",
            "report" => ".json",
            _ => throw new ArgumentException("Invalid PDF archive format."),
        };
        return Path.Combine(DirectoryPath(configuration), hash.ToUpperInvariant() + suffix);
    }

    public static PdfPageReference? FindPage(string text, string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            return null;
        string? hash = null;
        foreach (
            Match part in Regex.Matches(
                text,
                @"\[PDF DOCUMENT (?<hash>[A-F0-9]{64}); TEXT VERSION \d+\]|\[PDF PAGE (?<page>\d+)\](?<body>.*?)(?=\[PDF PAGE |\[PDF DOCUMENT |\[END PDF\]|\z)",
                RegexOptions.Singleline
            )
        )
        {
            if (part.Groups["hash"].Success)
                hash = part.Groups["hash"].Value;
            else if (hash is not null && TextEvidence.Contains(part.Groups["body"].Value, evidence))
                return new(
                    hash,
                    int.Parse(
                        part.Groups["page"].Value,
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                );
        }
        return null;
    }

    public static async Task ArchiveAsync(
        IConfiguration configuration,
        byte[] bytes,
        string hash,
        string original,
        string readable,
        IReadOnlyList<PdfPageText> pages,
        string? error,
        CancellationToken ct
    )
    {
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(DirectoryPath(configuration));
        else
            Directory.CreateDirectory(
                DirectoryPath(configuration),
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        await WriteAsync(FilePath(configuration, hash, "source"), bytes, ct);
        await WriteAsync(FilePath(configuration, hash, "original"), DiagnosticBytes(original), ct);
        await WriteAsync(FilePath(configuration, hash, "readable"), DiagnosticBytes(readable), ct);
        var report = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                TextVersion,
                Hash = hash,
                At = DateTimeOffset.UtcNow,
                Complete = error is null,
                Error = error,
                RequiresVisualReview = readable.Contains(ReviewMarker, StringComparison.Ordinal),
                Pages = pages.Select(x => new
                {
                    x.Page,
                    x.Method,
                    x.Confidence,
                    Characters = x.Text.Length,
                }),
                Notice = "OCR is machine transcription; verify critical values against the archived source PDF.",
            },
            new JsonSerializerOptions { WriteIndented = true }
        );
        await WriteAsync(FilePath(configuration, hash, "report"), report, ct);
    }

    private static byte[] DiagnosticBytes(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxTextBytes)
            text = text[..Math.Min(text.Length, MaxTextBytes / 4)];
        return Encoding.UTF8.GetBytes(text);
    }

    private static async Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, ct);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
