using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HiringPs.Core;

/// <summary>يسجل انتقالات المجال في جدول التدقيق الموجود، داخل نفس حفظ الحالة وبلا شبكة أو حفظ إضافي.</summary>
public sealed class OperationTracking
{
    private readonly List<AuditEntry> staged = [];

    /// <summary>يجهز أحداث الحفظ الحالي ويزيل نسخته غير المحفوظة عند إعادة المحاولة، دون تكرارها.</summary>
    public void Stage(HiringDb db)
    {
        foreach (var pending in staged)
            if (db.Entry(pending).State == EntityState.Added)
                db.Entry(pending).State = EntityState.Detached;
        staged.Clear();
        db.ChangeTracker.DetectChanges();
        var actor = db.ChangeTracker.Entries<AuditEntry>().LastOrDefault(x => x.State == EntityState.Added)?.Entity.Actor ?? "System";
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
        {
            var value = Describe(entry);
            if (value is not { } item)
                continue;
            var audit = Create(item.Area, item.Outcome, item.Name, item.Detail, actor);
            staged.Add(audit);
            db.Audit.Add(audit);
        }
    }

    /// <summary>ينشئ حدثًا صريحًا للمسارات التي تستخدم تحديث SQL جماعيًا ولا يراها متتبع EF.</summary>
    public static AuditEntry Create(OperationArea area, OperationOutcome outcome, string name, string detail, string actor = "Worker") =>
        new()
        {
            Actor = actor.Truncate(200),
            Action = $"Activity.{area}.{outcome}.{name}".Truncate(200),
            Detail = detail.Truncate(2000)
        };

    /// <summary>يميز التغيير الفعلي في الحقل عن تحميل كيان أو تجديد حجزه فقط.</summary>
    private static bool Changed(EntityEntry e, string property) => e.State == EntityState.Added
        || (e.State == EntityState.Modified && e.Property(property).IsModified);

    /// <summary>يختار انتقالات ذات معنى ويستبعد الحمولات والأسرار ونبضات الحياة الدورية.</summary>
    private static (OperationArea Area, OperationOutcome Outcome, string Name, string Detail)? Describe(EntityEntry e)
    {
        var added = e.State == EntityState.Added;
        var deleted = e.State == EntityState.Deleted;
        switch (e.Entity)
        {
            case Source x when added || deleted:
                return (OperationArea.Sources, OperationOutcome.Succeeded, added ? "Created" : "Deleted", (added ? $"New source; {x.Name.Truncate(200)}" : $"Source {x.Id}; {x.Name.Truncate(200)}"));
            case Opportunity x when added || deleted || Changed(e, nameof(x.Status)):
                return (OperationArea.Opportunities, OperationOutcome.Succeeded, added ? "Created" : deleted ? "Deleted" : "StatusChanged", (added ? $"New opportunity; {x.Title.Truncate(200)}; {x.Status}" : $"Opportunity {x.Id}; {x.Title.Truncate(200)}; {x.Status}"));
            case ScrapeRun x when added || Changed(e, nameof(x.FinishedAt)):
                return (OperationArea.Collection, added ? OperationOutcome.Started : x.Status == "Success" ? OperationOutcome.Succeeded : OperationOutcome.Failed,
                    "Run", (added ? $"Source {x.SourceId}; Started" : $"Run {x.Id}; Source {x.SourceId}; Found {x.Found}; Changed {x.Changed}; {x.Status}"));
            case SourceItem x when Changed(e, nameof(x.Status)) && x.Status is ProcessingStatus.Retry or ProcessingStatus.NeedsReview:
                return (OperationArea.Collection, x.Status == ProcessingStatus.NeedsReview ? OperationOutcome.Pending : OperationOutcome.Failed,
                    "Item", $"Item {x.Id}; Source {x.SourceId}; {x.Status}");
            case SocialPublication x when added || Changed(e, nameof(x.Status)) || Changed(e, nameof(x.Error)) || Changed(e, nameof(x.NextAttemptAt)):
                return (x.Provider == "Telegram" ? OperationArea.Telegram : OperationArea.Publishing,
                    x.Status == "Published" ? OperationOutcome.Succeeded : x.Status == "Skipped" ? OperationOutcome.Skipped
                    : x.Status == "Unknown" ? OperationOutcome.Uncertain : x.Status == "Failed" || x.Error.Length > 0 ? OperationOutcome.Failed : OperationOutcome.Pending,
                    "Publish", $"Publication {x.Id}; Opportunity {x.OpportunityId}; {x.Provider}; {x.Status}; Attempt {x.Attempts}; Code {x.Error}");
            case MemberMessage x when added || Changed(e, nameof(x.SentAt)) || Changed(e, nameof(x.CancelledAt)) || Changed(e, nameof(x.AvailableAt)):
                return (OperationArea.Telegram, x.SentAt != null ? OperationOutcome.Succeeded : x.CancelledAt != null ? OperationOutcome.Skipped
                    : x.LastError.Length > 0 ? OperationOutcome.Failed : OperationOutcome.Pending,
                    "BotDelivery", (added ? $"New message; {x.Kind}; Opportunity {x.OpportunityId}" : $"Message {x.Id}; {x.Kind}; Opportunity {x.OpportunityId}; Attempt {x.Attempts}; Code {x.LastError}"));
            case TelegramInbox x when added || Changed(e, nameof(x.ProcessedAt)):
                return (OperationArea.Telegram, x.ProcessedAt != null ? OperationOutcome.Succeeded : OperationOutcome.Pending, "Update", $"Update {x.Id}");
            case PaymentOrder x when added || Changed(e, nameof(x.State)):
                return (OperationArea.Payments, x.State is PaymentState.Paid or PaymentState.ManuallyApproved ? OperationOutcome.Succeeded
                    : x.State == PaymentState.Failed ? OperationOutcome.Failed : OperationOutcome.Pending,
                    "Order", $"Order {x.Id}; {x.Gateway}; {x.GatewayEnvironment}; {x.State}");
            case AiUsage x when added || Changed(e, nameof(x.FinishedAt)):
                return (OperationArea.Ai, x.Success == true ? OperationOutcome.Succeeded : x.Success == false ? OperationOutcome.Failed : OperationOutcome.Started,
                    "Request", (added ? $"New AI request; SourceItem {x.SourceItemId}" : $"AI {x.Id}; SourceItem {x.SourceItemId}; HTTP {x.HttpStatus}; Tokens {x.TotalTokens}"));
            case ToolCheckResult x when added:
                return (OperationArea.Tools, x.State is ToolState.Problem or ToolState.NotConfigured ? OperationOutcome.Failed : OperationOutcome.Succeeded,
                    "Check", $"{x.Tool}; {x.State}; {x.Code}; Request {x.RequestId}; Worker {x.WorkerId}; {x.Detail}");
        }
        return null;
    }
}
