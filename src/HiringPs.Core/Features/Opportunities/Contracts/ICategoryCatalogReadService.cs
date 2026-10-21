namespace HiringPs.Core;

/// <summary>قراءة التسميات المتاحة لمحررات الفرص دون ربط واجهة HTTP بمصدر التخزين.</summary>
public interface ICategoryCatalogReadService
{
    /// <summary>يعيد أسماء المجالات الموجودة دون محتوى الفرص أو بيانات أصحابها.</summary>
    Task<string[]> OptionsAsync(CancellationToken ct);
}
