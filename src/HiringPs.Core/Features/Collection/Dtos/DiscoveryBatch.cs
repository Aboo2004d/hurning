using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>DTO لدفعة اكتشاف مع مؤشر استئناف بدل ربط العميل بكيان EF مباشرة.</summary>
public record DiscoveryBatch(
    IReadOnlyList<(string Id, string Url)> Items,
    string? NextUrl,
    int CompletedPages,
    int ReplayClicks = 0
);
