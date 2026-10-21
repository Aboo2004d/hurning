using HiringPs.Core;

/// <summary>كتالوج ثابت لاختبار مسار نموذج الإنشاء الحقيقي دون SQL.</summary>
public sealed class FixtureCategoryCatalog : ICategoryCatalogReadService
{
    public Task<string[]> OptionsAsync(CancellationToken ct) => Task.FromResult(new[] { "Programming", "هندسة", "محاسبة" });
}
