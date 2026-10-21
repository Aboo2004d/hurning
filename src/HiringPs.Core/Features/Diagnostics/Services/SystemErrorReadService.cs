using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة الأخطاء دون تتبع أو تعديل، مع ترشيح SQL قبل ترقيم النتائج.</summary>
public sealed class SystemErrorReadService(HiringDb db)
{
    /// <summary>يبحث بالمتابعة والمسار والرسالة والمصدر، ويطبق حدود أيام فلسطين كاملة.</summary>
    public IQueryable<SystemErrorSummaryDto> Query(SystemErrorFilterDto filter)
    {
        var query = db.ApplicationErrors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().Truncate(200);
            query = query.Where(x => x.TraceId.Contains(search) || x.Path.Contains(search)
                || x.Message.Contains(search) || x.Category.Contains(search) || x.ExceptionType.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(filter.Application))
            query = query.Where(x => x.Application == filter.Application);
        if (!string.IsNullOrWhiteSpace(filter.Level))
            query = query.Where(x => x.Level == filter.Level);
        if (filter.From is { } from)
        {
            var start = OperationReadService.PalestineDayStart(from);
            query = query.Where(x => x.At >= start);
        }
        if (filter.To is { } to && to < DateOnly.MaxValue)
        {
            var end = OperationReadService.PalestineDayStart(to.AddDays(1));
            query = query.Where(x => x.At < end);
        }
        return query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id)
            .Select(x => new SystemErrorSummaryDto(x.Id, x.At, x.Application, x.Level, x.Category,
                x.TraceId, x.Path, x.ExceptionType, x.Message));
    }

    /// <summary>يحمل تفاصيل سجل واحد فقط عند طلبها، أو يعيد null للسجل غير الموجود.</summary>
    public Task<SystemErrorDetailsDto?> FindAsync(long id, CancellationToken ct) => db.ApplicationErrors
        .AsNoTracking().Where(x => x.Id == id).Select(x => new SystemErrorDetailsDto(
            new SystemErrorSummaryDto(x.Id, x.At, x.Application, x.Level, x.Category,
                x.TraceId, x.Path, x.ExceptionType, x.Message), x.Details)).SingleOrDefaultAsync(ct);
}
