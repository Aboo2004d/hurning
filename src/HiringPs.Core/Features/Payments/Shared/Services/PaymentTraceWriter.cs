using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

// Store diagnostic codes, order IDs and whitelisted receipt numbers only. Never provider bodies,
// authorization headers, tokens, OTPs, phone numbers or exception messages.
/// <summary>يحفظ تشخيص الدفع بالرموز والمعرّفات والحقائق المسموح بها دون مفاتيح أو OTP أو بيانات مزود خام.</summary>
public sealed class PaymentTraceWriter(
    IServiceScopeFactory scopes,
    IHostEnvironment environment,
    ILogger<PaymentTraceWriter> logger
)
{
    public const string BuildMarker = "payment-trace-sandbox-net-v5";
    public string? LastTraceId { get; private set; }

    /// <summary>يفتح جلسة تشخيص مرتبطة بمعرّف طلب الدفع.</summary>
    public PaymentTraceSession Start(Guid orderId)
    {
        var session = new PaymentTraceSession(this, orderId);
        LastTraceId = session.Id.ToString("N");
        session.Step("Start", BuildMarker);
        return session;
    }

    /// <summary>يسجل رموز التشخيص المسموح بها دون تفاصيل حساسة من المزود.</summary>
    internal void Log(PaymentTraceSession session, string stage, string code, int? httpStatus) =>
        logger.LogInformation(
            "PaymentTrace {TraceId} Order {OrderId} App {Application} Stage {Stage} Code {Code} HTTP {HttpStatus} Build {Build}",
            session.Id.ToString("N"),
            session.OrderId,
            environment.ApplicationName,
            stage,
            code,
            httpStatus,
            BuildMarker
        );

    /// <summary>يحفظ أثر تشخيص الدفع وفق بيئة التشغيل وسياسة التسجيل الحالية.</summary>
    internal async Task SaveAsync(PaymentTraceSession session)
    {
        if (!session.Persist)
            return;
        try
        {
            // Independent context: recording a failure must never save partial membership work.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
            db.Audit.Add(
                new AuditEntry
                {
                    Actor = "Payment:" + session.OrderId.ToString("N"),
                    Action = "Payment.VerifyTrace",
                    Detail = JsonSerializer
                        .Serialize(
                            new
                            {
                                TraceId = session.Id.ToString("N"),
                                Build = BuildMarker,
                                App = environment.ApplicationName,
                                HostEnvironment = environment.EnvironmentName,
                                session.OrderId,
                                session.OrderEnvironment,
                                session.RuntimeEnvironment,
                                session.Gateway,
                                session.AdapterEnvironment,
                                session.ReceiptFacts,
                                Steps = session.Steps,
                            }
                        )
                        .Truncate(2000),
                }
            );
            await db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "PaymentTrace {TraceId} StorageUnavailable {ExceptionType}",
                session.Id.ToString("N"),
                ex.GetType().Name
            );
        }
    }
}
