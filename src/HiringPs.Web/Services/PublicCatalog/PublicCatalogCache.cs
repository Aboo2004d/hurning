using HiringPs.Core;
using Microsoft.Extensions.Primitives;

namespace HiringPs.Web.Models;

/// <summary>نسخة كاش مشتركة بين الإدارة والعامل داخل الويب، تتجدد بعد الحفظ أو تغير يوم فلسطين.</summary>
public sealed class PublicCatalogCache(Func<DateOnly>? calendar = null) : IPublicCatalogInvalidator
{
    private readonly object gate = new();
    private readonly Func<DateOnly> calendar = calendar ?? TextRules.PalestineToday;
    private DateOnly day = (calendar ?? TextRules.PalestineToday)();
    private string version = Guid.NewGuid().ToString("N");
    private CancellationTokenSource changes = new();

    public (string Version, IChangeToken Changed) Snapshot()
    {
        lock (gate)
        {
            if (day != calendar()) Reset();
            return (version, new CancellationChangeToken(changes.Token));
        }
    }

    /// <summary>يلغي إدخالات البيانات القديمة ويغير مفتاح كاش الصفحة دون استعلام تحقق إضافي.</summary>
    public void Invalidate()
    {
        lock (gate) Reset();
    }

    private void Reset()
    {
        var old = changes;
        changes = new();
        version = Guid.NewGuid().ToString("N");
        day = calendar();
        old.Cancel();
        old.Dispose();
    }
}
