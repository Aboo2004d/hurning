using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

internal static class CollectorDiagnosticRetentionChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool valid, string label) { if (!valid) throw new Exception(label); count++; Console.WriteLine("PASS " + label); }
        var folder = Path.Combine(Path.GetTempPath(), "diagnostic-retention-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Diagnostics:Directory"] = folder }).Build();
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Audit (Id INTEGER PRIMARY KEY AUTOINCREMENT, Actor TEXT NOT NULL, Action TEXT NOT NULL, Detail TEXT NOT NULL, At TEXT NOT NULL)");
        var report = new CollectorDiagnostic { SourceId = 9, Operation = "Discovery", Stage = "WaitForAccess", HttpStatus = 403,
            Title = "Just a moment...", ReadyState = "complete", RequestedUrl = "https://works.ps/jobs", FinalUrl = "https://works.ps/jobs",
            SelectorMatches = 0, SelectorVisible = false, Screenshot = true,
            Network = [new("https://works.ps/jobs", "document", "HttpFailure", 403)] };
        db.Audit.Add(new() { Action = "Collector.Diagnostic", Detail = CollectorDiagnosticFiles.AuditDetail(report) }); await db.SaveChangesAsync();
        var reader = new CollectorDiagnosticReadService(db, configuration);
        var retained = (await reader.ReadAsync(report.Id, default))!;
        Check(retained.Title == report.Title && retained.ReadyState == "complete" && retained.RequestedUrl == report.RequestedUrl,
            "Missing capture files retain page title, readiness and requested URL from the database audit");
        Check(retained.HttpStatus == 403 && retained.Network.Single().Status == 403 && retained.SelectorMatches == 0 && retained.SelectorVisible == false,
            "Audit fallback preserves observed HTTP, request and selector evidence");
        Check(!retained.Screenshot && retained.CaptureNote.Contains("this instance") && !retained.CaptureNote.Contains("storage unavailable"),
            "A file missing on this instance does not falsely establish a storage-permission failure or advertise a missing screenshot");
        var legacy = new CollectorDiagnostic { SourceId = 8, Operation = "Discovery", Stage = "WaitForAccess" };
        using var legacyJson = JsonDocument.Parse(CollectorDiagnosticFiles.AuditDetail(legacy));
        var fields = legacyJson.RootElement.EnumerateObject().Where(x => x.Name is not ("Title" or "ReadyState" or "requestedUrl" or "Network" or "SelectorMatches" or "SelectorVisible")).ToDictionary(x => x.Name, x => x.Value.Clone());
        db.Audit.Add(new() { Action = "Collector.Diagnostic", Detail = JsonSerializer.Serialize(fields) }); await db.SaveChangesAsync();
        Check((await reader.ReadAsync(legacy.Id, default)) is { Title: "", ReadyState: "", Network.Count: 0 }, "Old diagnostic audits remain readable");
        report.Title = new string('ع', 500); report.Selector = new string('ع', 500); report.CaptureNote = new string('ع', 500);
        report.FinalUrl = "https://works.ps/" + new string('ع', 500) + "?token=private"; report.RequestedUrl = report.FinalUrl;
        var bounded = CollectorDiagnosticFiles.AuditDetail(report);
        using var parsed = JsonDocument.Parse(bounded);
        Check(bounded.Length <= 2000 && parsed.RootElement.GetProperty("id").GetString() == report.Id.ToString("N") && !bounded.Contains("private"),
            "Unicode-heavy metadata remains valid JSON within the database limit and redacts URL secrets");
        Check(await reader.ReadAsync(Guid.NewGuid(), default) is null, "Unrecorded diagnostic IDs remain unavailable");
        Directory.CreateDirectory(folder);
        try
        {
            report.Title = "Full capture";
            await File.WriteAllTextAsync(CollectorDiagnosticFiles.FilePath(configuration, report.Id), JsonSerializer.Serialize(report, CollectorDiagnosticFiles.Json));
            Check((await reader.ReadAsync(report.Id, default))!.Title == "Full capture", "Existing full capture continues to take precedence");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine($"Diagnostic retention: {count} checks passed using isolated SQLite and temporary files.");
    }
}
