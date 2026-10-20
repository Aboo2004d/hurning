using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يحجز طلب جمع يدوي بعد موافقة استخدام المصدر دون تشغيل المتصفح في HTTP.</summary>
public sealed class SourceCollectionRequestService(HiringDb db)
{
    /// <summary>يعيد رمز الرسالة أو null للمصدر المفقود؛ الطلب الجاري لا يكرر الحجز.</summary>
    public async Task<string?> QueueAsync(int id, string actor, CancellationToken ct)
    {
        var source = await db.Sources.FindAsync([id], ct);
        if (source is null)
            return null;
        if (!source.Enabled || !source.UsageApproved || source.ApprovalNote.Length == 0)
        {
            return "CollectNotApproved";
        }

        var requests = db.Set<SourceCollectionRequest>();
        var request = await requests.FindAsync([id], ct);
        if (request?.Status is "Pending" or "Running")
        {
            return "CollectAlreadyQueued";
        }

        if (request is null)
        {
            request = new() { SourceId = id };
            requests.Add(request);
        }

        request.RequestToken = Guid.NewGuid();
        request.RequestedAt = DateTimeOffset.UtcNow;
        request.StartedAt = null;
        request.FinishedAt = null;
        request.DiscoveryFinished = false;
        request.Status = "Pending";
        request.Error = "";
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "Source.CollectNow", Detail = $"Source {id}; token {request.RequestToken}" });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return "CollectAlreadyQueued";
        }

        return "CollectQueued";
    }
}
