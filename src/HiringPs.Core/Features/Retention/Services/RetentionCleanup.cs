using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>يحذف نسخ المصادر وملفات التشخيص المؤهلة وفق سياسة الاحتفاظ دون المساس بالأدوات.</summary>
public static class RetentionCleanup
{
    /// <summary>ينفذ عملية الخدمة ضمن حدود الإلغاء والموارد مع الحفاظ على فشلها الفعلي.</summary>
    public static async Task RunAsync(HiringDb db, IConfiguration config, CancellationToken ct)
    {
        var policy = await db.RetentionPolicies.SingleAsync(x => x.Id == 1, ct);
        if (!policy.Enabled || policy.LastCleanupAt > DateTimeOffset.UtcNow.AddHours(-24))
            return;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-policy.RevisionDays);
        policy.LastDeletedRevisions = await db
            .Revisions.Where(x => x.CapturedAt < cutoff)
            .ExecuteDeleteAsync(ct);
        var path = CollectorDiagnosticFiles.DirectoryPath(config);
        var removed = 0;
        if (Directory.Exists(path))
        {
            foreach (
                var file in new DirectoryInfo(path)
                    .EnumerateFiles()
                    .Where(x =>
                        x.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-policy.DiagnosticDays)
                    )
                    .Take(200)
            )
            {
                ct.ThrowIfCancellationRequested();
                if (
                    file.Extension is not (".jpg" or ".json")
                    || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "N", out _)
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0
                )
                    continue;
                try
                {
                    file.Delete();
                    removed++;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        policy.LastDeletedDiagnostics = removed;
        policy.LastCleanupAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
