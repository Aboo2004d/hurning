using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يزيد إصدار الكتالوج ذريًا داخل معاملة الحفظ الحالية دون استدعاء SaveChanges تكراريًا.</summary>
public sealed class PublicCatalogRevisionWriter
{
    private const string Update = "UPDATE [PublicCatalogRevisions] SET [Version] = [Version] + 1 WHERE [Id] = 1";
    /// <summary>يزيد الإصدار داخل المعاملة المتزامنة ويكشف غياب صف الماجريشن.</summary>
    public void Increment(HiringDb db)
    {
        if (db.Database.ExecuteSqlRaw(Update) != 1) throw new InvalidOperationException("Public catalog revision row is missing. Apply migrations first.");
    }
    /// <summary>يزيد الإصدار داخل معاملة حفظ البيانات غير المتزامنة.</summary>
    public async Task IncrementAsync(HiringDb db, CancellationToken ct)
    {
        if (await db.Database.ExecuteSqlRawAsync(Update, ct) != 1) throw new InvalidOperationException("Public catalog revision row is missing. Apply migrations first.");
    }
}
