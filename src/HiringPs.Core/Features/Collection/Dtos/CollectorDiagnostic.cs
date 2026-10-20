using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>DTO لتشخيص جولة جمع؛ ليس كيان قاعدة ولا يحمل صلاحية تنفيذ عملية.</summary>
public class CollectorDiagnostic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SourceId { get; set; }
    public string Operation { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string Stage { get; set; } = "";
    public string Selector { get; set; } = "";
    public int TimeoutMs { get; set; }
    public long ElapsedMs { get; set; }
    public long StageElapsedMs { get; set; }
    public string RequestedUrl { get; set; } = "";
    public string FinalUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public int? HttpStatus { get; set; }
    public string ReadyState { get; set; } = "";
    public int? SelectorMatches { get; set; }
    public bool? SelectorVisible { get; set; }
    public string ExceptionType { get; set; } = "";
    public bool Screenshot { get; set; }
    public string CaptureNote { get; set; } = "";
    public List<NetworkDiagnostic> Network { get; set; } = [];
}
