// إبلاغ محركات البحث عبر IndexNow بتغير صفحات الفرص العامة، على دفعات من العامل وليس داخل طلبات الويب.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون IndexNowQueue ضمن مجال Indexing؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class IndexNowQueue
{
    private static readonly string[] SignificantFields =
    [
        nameof(Opportunity.Kind),
        nameof(Opportunity.Title),
        nameof(Opportunity.Organization),
        nameof(Opportunity.Summary),
        nameof(Opportunity.Locations),
        nameof(Opportunity.Deadline),
        nameof(Opportunity.ApplyUrl),
        nameof(Opportunity.ClosureEvidence),
    ];

    /// <summary>يحدد تغييرات الفرصة التي تتطلب إخطار IndexNow.</summary>
    public static bool Affects(EntityEntry<Opportunity> entry)
    {
        if (entry.State == EntityState.Added)
            return entry.Entity.Status == PublicationStatus.Published;
        if (entry.State != EntityState.Modified)
            return false;
        var original = entry.Property(x => x.Status).OriginalValue;
        var current = entry.Entity.Status;
        if (original != current)
            return original == PublicationStatus.Published || current == PublicationStatus.Published;
        return current == PublicationStatus.Published
            && SignificantFields.Any(name =>
            {
                var property = entry.Property(name);
                return property.IsModified && !Equals(property.OriginalValue, property.CurrentValue);
            });
    }

    /// <summary>يحدث وقت إخطار الرابط عند تغيير حقل يؤثر على الصفحة العامة.</summary>
    public static void Touch(HiringDb db, Opportunity item)
    {
        var now = DateTimeOffset.UtcNow;
        var existing =
            db.IndexNowEntries.Local.FirstOrDefault(x =>
                x.Opportunity == item || (item.Id != 0 && x.OpportunityId == item.Id)
            ) ?? (item.Id != 0 ? db.IndexNowEntries.Find(item.Id) : null);
        if (existing is not null)
            existing.QueuedAt = now;
        else
            db.IndexNowEntries.Add(new IndexNowEntry { Opportunity = item, QueuedAt = now });
    }
}
