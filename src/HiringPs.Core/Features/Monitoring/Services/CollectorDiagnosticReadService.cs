using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>Reads diagnostic capture files only when their existing audit record permits discovery.</summary>
public sealed class CollectorDiagnosticReadService(HiringDb db, IConfiguration configuration)
{
    /// <summary>Loads the matching capture or its retained audit metadata; never contacts the source site.</summary>
    public async Task<CollectorDiagnostic?> ReadAsync(Guid id, CancellationToken ct)
    {
        var audit = await db
            .Audit.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Action == "Collector.Diagnostic" && x.Detail.Contains(id.ToString("N")),
                ct
            );
        if (audit is null)
            return null;
        var file = CollectorDiagnosticFiles.FilePath(configuration, id);
        if (File.Exists(file))
        {
            var report = JsonSerializer.Deserialize<CollectorDiagnostic>(
                await File.ReadAllTextAsync(file, ct),
                CollectorDiagnosticFiles.Json
            );
            if (report is null || report.Id != id)
                return null;
            return report;
        }

        using var metadata = JsonDocument.Parse(audit.Detail);
        var root = metadata.RootElement;
        return new CollectorDiagnostic
        {
            Id = id,
            At = root.GetProperty("At").GetDateTimeOffset(),
            SourceId = root.GetProperty("SourceId").GetInt32(),
            Stage = root.GetProperty("Stage").GetString() ?? "",
            Operation = root.GetProperty("Operation").GetString() ?? "",
            HttpStatus =
                    root.GetProperty("HttpStatus").ValueKind == JsonValueKind.Number
                        ? root.GetProperty("HttpStatus").GetInt32()
                        : null,
            FinalUrl = root.GetProperty("finalUrl").GetString() ?? "",
            Selector = root.GetProperty("selector").GetString() ?? "",
            TimeoutMs = root.GetProperty("TimeoutMs").GetInt32(),
            ElapsedMs = root.GetProperty("ElapsedMs").GetInt64(),
            StageElapsedMs = root.GetProperty("StageElapsedMs").GetInt64(),
            ExceptionType = root.GetProperty("ExceptionType").GetString() ?? "",
            CaptureNote = "Capture storage unavailable; metadata only.",
        };
    }

    /// <summary>Returns an existing screenshot path only after checking the diagnostic audit record.</summary>
    public async Task<string?> ImagePathAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Audit.AnyAsync(x => x.Action == "Collector.Diagnostic" && x.Detail.Contains(id.ToString("N")), ct))
            return null;
        var path = CollectorDiagnosticFiles.FilePath(configuration, id, true);
        return File.Exists(path) ? path : null;
    }
}
