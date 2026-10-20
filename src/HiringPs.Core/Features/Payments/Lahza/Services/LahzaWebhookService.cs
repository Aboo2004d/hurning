using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يتحقق من توقيع إشعار لحظة ثم يدرج العمل للتحقق من API؛ لا يمنح اشتراكًا من حمولة الإشعار.</summary>
public sealed class LahzaWebhookService(HiringDb db, IConfiguration configuration, LahzaGateway gateway,
    ILogger<LahzaWebhookService>? logger = null)
{
    /// <summary>يعالج حدثًا موقعًا ضمن حد الحجم، ويحفظ طلب التحقق قبل الإقرار باستقبال الإشعار.</summary>
    public async Task<LahzaWebhookOutcome> ProcessAsync(byte[] body, bool headerValid, string signature,
        string headerName, CancellationToken ct)
    {
        if (!gateway.Ready)
        {
            logger?.LogWarning(
                "PaymentWebhook Configuration {Code} Environment {Environment} Build {Build}",
                gateway.ConfigurationStatus,
                gateway.Environment,
                PaymentTraceWriter.BuildMarker
            );
            return LahzaWebhookOutcome.Unauthorized;
        }
        if (body.Length > 65536) return LahzaWebhookOutcome.InvalidRequest;
        if (
            !headerValid
            || !LahzaGateway.VerifySignature(
                body,
                signature,
                configuration["Payments:Lahza:" + gateway.Environment + ":SecretKey"] ?? ""
            )
        )
        {
            logger?.LogWarning(
                "PaymentWebhook InvalidSignature Environment {Environment} Build {Build} SignatureHeader {SignatureHeader}",
                gateway.Environment,
                PaymentTraceWriter.BuildMarker,
                headerName
            );
            return LahzaWebhookOutcome.Unauthorized;
        }
        try
        {
            using var json = JsonDocument.Parse(body);
            if (
                json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("event", out var kind)
                || kind.ValueKind != JsonValueKind.String
            )
                return LahzaWebhookOutcome.InvalidRequest;
            if (kind.GetString() != "charge.success")
                return LahzaWebhookOutcome.Accepted;
            if (
                !json.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("reference", out var field)
                || field.ValueKind != JsonValueKind.String
            )
                return LahzaWebhookOutcome.InvalidRequest;
            var reference = field.GetString();
            // The signed event only schedules verification: never grant from its body.
            // Save the work before acknowledging; repeated events are harmless.
            var matched = await db
                .PaymentOrders.Where(x =>
                    x.Gateway == "Lahza"
                    && x.GatewayEnvironment == gateway.Environment
                    && x.ProviderReference == reference
                    && x.State != PaymentState.Paid
                    && x.State != PaymentState.ManuallyApproved
                )
                .ExecuteUpdateAsync(
                    update =>
                        update
                            .SetProperty(x => x.State, PaymentState.Pending)
                            .SetProperty(x => x.LastCheckedAt, (DateTimeOffset?)null),
                    ct
                );
            Guid.TryParseExact(
                reference?.StartsWith("hiring-", StringComparison.Ordinal) is true
                    ? reference[7..]
                    : "",
                "N",
                out var orderId
            );
            logger?.LogInformation(
                "PaymentWebhook Accepted Order {OrderId} MatchedOrders {Count} Environment {Environment} Build {Build} SignatureHeader {SignatureHeader}",
                orderId,
                matched,
                gateway.Environment,
                PaymentTraceWriter.BuildMarker,
                headerName
            );
            return LahzaWebhookOutcome.Accepted;
        }
        catch (JsonException)
        {
            logger?.LogWarning("PaymentWebhook MalformedJson");
            return LahzaWebhookOutcome.InvalidRequest;
        }
    }
}
