using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يحجز النشر ذريًا قبل الاتصال ويسجل النتيجة حتى عند انقطاع المتصفح.</summary>
public sealed class SocialPublicationService(HiringDb db, SocialPublishing publishing, ILogger logger)
{
    /// <summary>يتحقق من الحالة والقالب والربط داخل القفل؛ لا يعيد إرسال محاولة معلقة أو مجهولة.</summary>
    public async Task<SocialPublicationCommandResultDto> PublishAsync(SocialPublicationCommandDto input, CancellationToken ct)
    {
        SocialPublication attempt;
        // Reserve before sending: concurrent clicks/replays cannot perform duplicate external requests.
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var resource = "SocialPublish:" + input.OpportunityId + ":" + input.Provider;
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @lockResult int; EXEC @lockResult=sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @lockResult < 0 THROW 51000, 'Social publication busy', 1;", ct);
            var item = await db.Opportunities.SingleOrDefaultAsync(x => x.Id == input.OpportunityId, ct);
            if (item is null || !SocialPublishing.Eligible(item, TextRules.PalestineToday())) return new(SocialPublicationRoute.InvalidRequest);
            if (await db.SocialPublications.AnyAsync(x => x.Id == input.Id, ct)) return new(SocialPublicationRoute.Compose);
            if (await db.SocialPublications.AnyAsync(x => x.OpportunityId == item.Id && x.Provider == input.Provider && (x.Status == "Pending" || x.Status == "Unknown"), ct))
            { return new(SocialPublicationRoute.Compose, "SocialUnknown"); }
            if (!input.ConfirmRepeat && await db.SocialPublications.AnyAsync(x => x.OpportunityId == item.Id && x.Provider == input.Provider && x.Status == "Published", ct))
            { return new(SocialPublicationRoute.Compose, "SocialRepeatRequired"); }
            var connection = await db.SocialConnections.SingleOrDefaultAsync(x => x.Provider == input.Provider, ct);
            if (connection is null || !connection.Enabled || connection.ExpiresAt <= DateTimeOffset.UtcNow) { return new(SocialPublicationRoute.Integrations, "IntegrationNotConfigured"); }
            var templateProvider = SocialPublishing.TemplateProvider(input.Provider);
            if (input.TemplateVersionId is not {} templateId || !await db.SocialTemplateVersions.AnyAsync(x => x.Id == templateId && x.Provider == templateProvider && (x.Scope == item.Kind.ToString() || x.Scope == "general"), ct)) return new(SocialPublicationRoute.InvalidRequest);
            var text = input.Text.Trim();
            var link = publishing.OpportunityUrl(item.Id, item.OriginalLanguage);
            // The canonical link is appended on the server, outside the editable text.
            text = text.Replace(link, "", StringComparison.Ordinal).Trim() + "\n\n" + link;
            if (text.Length > (SocialPublishing.IsLinkedIn(input.Provider) ? 3000 : 60000))
            { return new(SocialPublicationRoute.Compose, "SocialTextTooLong"); }
            attempt = new SocialPublication { Id = input.Id, OpportunityId = item.Id, Provider = input.Provider,
                PageId = connection.PageId, TemplateVersionId = input.TemplateVersionId, Text = text, Actor = input.Actor };
            db.SocialPublications.Add(attempt);
            db.Audit.Add(new AuditEntry { Actor = attempt.Actor, Action = "Social.Reserved", Detail = attempt.Id.ToString() });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        // A disconnected browser must not cancel recording a provider's result.
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        try { await publishing.PublishAsync(attempt, budget.Token); }
        catch (Exception ex) when (ex is InvalidOperationException or CryptographicException)
        {
            // Configuration and token decryption fail before sending to the provider.
            attempt.Status = "Failed";
            logger.LogWarning("SocialPublish Attempt {AttemptId} Provider {Provider} Stage PreparationFailed ExceptionType {ExceptionType}",
                attempt.Id, attempt.Provider, ex.GetType().Name);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException)
        {
            attempt.Status = "Unknown";
            // Provider bodies and exception messages can contain tokens or private content.
            logger.LogWarning("SocialPublish Attempt {AttemptId} Provider {Provider} Stage Unconfirmed ExceptionType {ExceptionType}",
                attempt.Id, attempt.Provider, ex.GetType().Name);
        }
        await db.SaveChangesAsync(CancellationToken.None);
        return new(SocialPublicationRoute.Compose, "Social" + attempt.Status);
    }
}
