using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

public record PdfPageText(int Page, string Method, string Text, double? Confidence = null);

public record PdfTextResult(string Hash, string Text, IReadOnlyList<PdfPageText> Pages);

public record PdfPageReference(string Hash, int Page);

public static class PdfTextFiles
{
    public const int TextVersion = 2;
    public const string ReviewMarker = "[PDF OCR REVIEW REQUIRED]";
    public const int MaxTextBytes = 2 * 1024 * 1024;

    public static string DirectoryPath(IConfiguration configuration)
    {
        var configured =
            configuration["Pdf:WorkDirectory"]
            ?? Path.Combine(Path.GetTempPath(), "hiring-pdf-work");
        var path = Path.GetFullPath(configured, AppContext.BaseDirectory);
        if (
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(x => x.Equals("wwwroot", StringComparison.OrdinalIgnoreCase))
        )
            throw new ContentNeedsReviewException("PDF work directory must be outside wwwroot.");
        return path;
    }

    public static void Prepare(IConfiguration configuration)
    {
        var root = DirectoryPath(configuration);
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(root);
        else
            Directory.CreateDirectory(
                root,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        // A crashed process releases the lock; keep live jobs and delete only old, app-owned directories.
        foreach (var directory in new DirectoryInfo(root).EnumerateDirectories())
        {
            if (
                !Guid.TryParseExact(directory.Name, "N", out _)
                || directory.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-1)
                || (directory.Attributes & FileAttributes.ReparsePoint) != 0
                || !File.Exists(Path.Combine(directory.FullName, ".active"))
            )
                continue;
            try
            {
                using (
                    new FileStream(
                        Path.Combine(directory.FullName, ".active"),
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.None
                    )
                ) { }
                directory.Delete(recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        CleanupLegacyArchives(configuration);
    }

    public static void CleanupLegacyArchives(IConfiguration configuration)
    {
        var oldRoot = Path.GetFullPath(
            configuration["Pdf:TextArchivePath"] ?? Path.Combine("logs", "pdf-text"),
            AppContext.BaseDirectory
        );
        if (
            !Directory.Exists(oldRoot)
            || (File.GetAttributes(oldRoot) & FileAttributes.ReparsePoint) != 0
        )
            return;
        foreach (var file in new DirectoryInfo(oldRoot).EnumerateFiles())
        {
            if (
                (file.Attributes & FileAttributes.ReparsePoint) == 0
                && Regex.IsMatch(
                    file.Name,
                    @"\A[A-Fa-f0-9]{64}(?:\.pdf|\.original\.txt|\.readable\.txt|\.json)(?:\.[A-Fa-f0-9]{32}\.tmp)?\z"
                )
            )
                file.Delete();
        }
    }

    public static async Task DeleteWorkAsync(string work)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(work))
                    Directory.Delete(work, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 2)
                    throw new ContentNeedsReviewException(
                        "PDF temporary file deletion failed; check worker permissions before retrying."
                    );
                await Task.Delay(100);
            }
        }
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
}
