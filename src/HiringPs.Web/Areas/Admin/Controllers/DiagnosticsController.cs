using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class DiagnosticsController : AdminControllerBase
{
    public DiagnosticsController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Diagnostic(
        Guid id,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var audit = await db
            .Audit.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Action == "Collector.Diagnostic" && x.Detail.Contains(id.ToString("N")),
                ct
            );
        if (audit is null)
            return NotFound();
        var file = CollectorDiagnosticFiles.FilePath(configuration, id);
        if (System.IO.File.Exists(file))
        {
            var report = JsonSerializer.Deserialize<CollectorDiagnostic>(
                await System.IO.File.ReadAllTextAsync(file, ct),
                CollectorDiagnosticFiles.Json
            );
            if (report is null || report.Id != id)
                return NotFound();
            page = AdminPager.Normalize(page, report.Network.Count);
            ViewData["NetworkPager"] = new AdminPager
            {
                Page = page,
                TotalRecords = report.Network.Count,
            };
            report.Network = report
                .Network.Skip((page - 1) * AdminPager.PageSize)
                .Take(AdminPager.PageSize)
                .ToList();
            return View(report);
        }

        using var metadata = JsonDocument.Parse(audit.Detail);
        var root = metadata.RootElement;
        return View(
            new CollectorDiagnostic
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
            }
        );
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DiagnosticImage(Guid id, CancellationToken ct)
    {
        if (
            !await db.Audit.AnyAsync(
                x => x.Action == "Collector.Diagnostic" && x.Detail.Contains(id.ToString("N")),
                ct
            )
        )
            return NotFound();
        var path = CollectorDiagnosticFiles.FilePath(configuration, id, true);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/jpeg") : NotFound();
    }
}
