using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

// Only fixed diagnostic codes and order identifiers are stored. Never provider bodies,
// authorization headers, tokens, OTPs, phone numbers or exception messages.
public sealed class PaymentTraceWriter(
    IServiceScopeFactory scopes,
    IHostEnvironment environment,
    ILogger<PaymentTraceWriter> logger
)
{
    public const string BuildMarker = "payment-trace-optional-domain-v1";
    public string? LastTraceId { get; private set; }

    public PaymentTraceSession Start(Guid orderId)
    {
        var session = new PaymentTraceSession(this, orderId);
        LastTraceId = session.Id.ToString("N");
        session.Step("Start", BuildMarker);
        return session;
    }

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

public sealed class PaymentTraceSession(PaymentTraceWriter writer, Guid orderId) : IAsyncDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid OrderId { get; } = orderId;
    public string? OrderEnvironment { get; set; }
    public string? RuntimeEnvironment { get; set; }
    public string? Gateway { get; set; }
    public string? AdapterEnvironment { get; set; }
    public bool Persist { get; set; } = true;
    public List<PaymentTraceStep> Steps { get; } = [];

    public void Step(string stage, string code, int? httpStatus = null)
    {
        if (Steps.Count < 12)
            Steps.Add(new(stage, code, httpStatus));
        writer.Log(this, stage, code, httpStatus);
    }

    public ValueTask DisposeAsync() => new(writer.SaveAsync(this));
}

public sealed record PaymentTraceStep(string Stage, string Code, int? HttpStatus);
