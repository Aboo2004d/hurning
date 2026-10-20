using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يجمع التدقيق الإداري وأحداث التشغيل في استعلام واحد قابل للبحث والترقيم داخل SQL.</summary>
public sealed class OperationReadService(HiringDb db)
{
    /// <summary>يسقط الحقول الآمنة ويطبق المرشحات قبل الترقيم؛ يحول حدود أيام فلسطين إلى UTC.</summary>
    public IQueryable<OperationSummaryDto> Query(OperationFilterDto filter)
    {
        var query = db.Audit.AsNoTracking().Select(x => new OperationSummaryDto
        {
            Id = x.Id,
            At = x.At,
            Actor = x.Actor,
            Action = x.Action,
            Detail = x.Detail,
            Area =
            x.Action.StartsWith("Activity.Opportunities.") || x.Action.StartsWith("Opportunity.") || x.Action.StartsWith("Report.") || x.Action.StartsWith("Duplicate.") ? OperationArea.Opportunities
            : x.Action.StartsWith("Activity.Sources.") || x.Action.StartsWith("Source.") ? OperationArea.Sources
            : x.Action.StartsWith("Activity.Collection.") || x.Action.StartsWith("Collector.") || x.Action.StartsWith("Item.") ? OperationArea.Collection
            : x.Action.StartsWith("Activity.Publishing.") || x.Action.StartsWith("Social.") || x.Action.StartsWith("SocialTemplate.") ? OperationArea.Publishing
            : x.Action.StartsWith("Activity.Telegram.") || x.Action.StartsWith("Telegram") ? OperationArea.Telegram
            : x.Action.StartsWith("Activity.Payments.") || x.Action.StartsWith("Payment") || x.Action.StartsWith("Wallet") ? OperationArea.Payments
            : x.Action.StartsWith("Activity.Ai.") || x.Action.StartsWith("Ai.") || x.Action.StartsWith("Prompt.") ? OperationArea.Ai
            : x.Action.StartsWith("Activity.Tools.") || x.Action.StartsWith("Tools.") ? OperationArea.Tools : OperationArea.System,
            Outcome = x.Action.StartsWith("Activity.")
                ? x.Action.Contains(".Failed.") ? OperationOutcome.Failed
                : x.Action.Contains(".Started.") ? OperationOutcome.Started
                : x.Action.Contains(".Pending.") ? OperationOutcome.Pending
                : x.Action.Contains(".Skipped.") ? OperationOutcome.Skipped
                : x.Action.Contains(".Uncertain.") ? OperationOutcome.Uncertain
                : x.Action.Contains(".Succeeded.") ? OperationOutcome.Succeeded : OperationOutcome.Unknown
                : OperationOutcome.Unknown,
        });
        if (filter.Area is { } area)
            query = query.Where(x => x.Area == area);
        if (filter.Outcome is { } outcome)
            query = query.Where(x => x.Outcome == outcome);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().Truncate(200);
            query = query.Where(x => x.Action.Contains(search) || x.Detail.Contains(search) || x.Actor.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(filter.Actor))
            query = query.Where(x => x.Actor == filter.Actor.Trim());
        if (filter.From is { } from)
        {
            var start = PalestineDayStart(from);
            query = query.Where(x => x.At >= start);
        }
        if (filter.To is { } to && to < DateOnly.MaxValue)
        {
            var end = PalestineDayStart(to.AddDays(1));
            query = query.Where(x => x.At < end);
        }
        return query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id);
    }

    /// <summary>يحول بداية اليوم الفلسطيني إلى لحظة عالمية دون تغيير تواريخ السجلات المخزنة.</summary>
    public static DateTimeOffset PalestineDayStart(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var zone = TextRules.PalestineTimeZone;
        // A clock jump can remove midnight; start at the first valid minute of that local day.
        while (zone.IsInvalidTime(local))
            local = local.AddMinutes(1);
        if (zone.IsAmbiguousTime(local))
            return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).ToUniversalTime();
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
