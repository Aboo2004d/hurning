using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

public static class ToolCheckQueue
{
    public static async Task<bool> EnqueueAsync(
        HiringDb db,
        IConfiguration config,
        ToolCheckKind kind,
        string actor,
        CancellationToken ct,
        DateTimeOffset? scheduledAt = null
    )
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentException("Invalid check kind.");
        await using var gate = new SqlRunLock(
            config.GetConnectionString("Hiring")!,
            "HiringPs.Tools.Queue"
        );
        if (!await gate.AcquireAsync(ct))
            return false;
        // Under the queue lock, an existing check after this midnight also
        // satisfies today's automatic request, including one from another worker.
        if (scheduledAt is { } due)
        {
            if (kind != ToolCheckKind.Standard)
                throw new ArgumentException("Only standard checks can be scheduled.");
            if (
                await db.ToolCheckRequests.AnyAsync(
                    x => x.Kind == ToolCheckKind.Standard && x.RequestedAt >= due,
                    ct
                )
            )
                return true;
        }
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
        if (
            await db.ToolCheckRequests.AnyAsync(
                x => x.Kind == kind && (x.ActiveSlot != null || x.RequestedAt > cutoff),
                ct
            )
        )
            return false;
        db.ToolCheckRequests.Add(
            new()
            {
                Kind = kind,
                Actor = actor.Truncate(200),
                ActiveSlot = (int)kind + 1,
            }
        );
        db.Audit.Add(
            new()
            {
                Actor = actor.Truncate(200),
                Action = "Tools.CheckQueued",
                Detail = kind.ToString(),
            }
        );
        await db.SaveChangesAsync(ct);
        return true;
    }

    public static async Task RunNextAsync(
        HiringDb db,
        IConfiguration config,
        ToolWorkerRuntime runtime,
        IServiceProvider services,
        CancellationToken ct
    )
    {
        await using var gate = new SqlRunLock(
            config.GetConnectionString("Hiring")!,
            "HiringPs.Tools.Runner"
        );
        if (!await gate.AcquireAsync(ct))
            return;
        // No other runner owns the session lock now. Never repeat an interrupted paid API probe automatically.
        await db
            .ToolCheckRequests.Where(x => x.Status == ToolRequestStatus.Running)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, ToolRequestStatus.Failed)
                        .SetProperty(x => x.ActiveSlot, (int?)null)
                        .SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow)
                        .SetProperty(x => x.Outcome, "Interrupted"),
                ct
            );
        var request = await db
            .ToolCheckRequests.Where(x => x.Status == ToolRequestStatus.Pending)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (request is null)
            return;
        request.Status = ToolRequestStatus.Running;
        request.WorkerId = runtime.Id;
        request.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        var keys =
            request.Kind == ToolCheckKind.Standard ? ToolMonitoringRules.StandardTools : ["AiLive"];
        var failed = false;
        foreach (var key in keys)
        {
            await gate.VerifyAsync(ct);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ToolProbe result;
            try
            {
                if (key == "AiLive")
                {
                    var active = await db
                        .WorkflowSettings.AsNoTracking()
                        .SingleAsync(x => x.Id == 1, ct);
                    var prompt = await db
                        .PromptVersions.AsNoTracking()
                        .SingleAsync(x => x.Id == active.ActivePromptVersionId, ct);
                    using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    budget.CancelAfter(TimeSpan.FromSeconds(60));
                    var extractor =
                        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AiExtractor>(
                            services
                        );
                    var reply = await extractor.ExtractAsync(
                        "Diagnostic connectivity sample only. No actual announcement or opportunity is provided. Return inScope:false and an empty opportunities array.",
                        budget.Token,
                        new(prompt, null)
                    );
                    result = reply.Opportunities is null
                        ? new(key, ToolState.Problem, "AiInvalidResponse")
                        : new(key, ToolState.Ready, "AiResponded");
                }
                else if (key is "LahzaPayment" or "JawwalPayPayment")
                    result = await Microsoft
                        .Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<PaymentToolProbes>(
                            services
                        )
                        .CheckAsync(key, ct);
                else
                    result = await new ToolProbes(config).CheckAsync(key, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (AiUnavailableException ex)
            {
                var status = System
                    .Text.RegularExpressions.Regex.Match(ex.Message, @"HTTP (\d{3})")
                    .Groups[1]
                    .Value;
                var code = status switch
                {
                    "401" or "403" => "AiAccessDenied",
                    "429" => "AiQuota",
                    "400" or "404" => "AiModelUnavailable",
                    _ => "AiUnavailable",
                };
                result = new(
                    key,
                    ToolState.Problem,
                    code,
                    status.Length == 3 ? "HTTP " + status : ""
                );
            }
            catch (AiTransientException)
            {
                result = new(key, ToolState.Problem, "AiNetworkFailed");
            }
            catch (OperationCanceledException)
            {
                result = new(key, ToolState.Problem, "CheckTimeout");
            }
            catch (Exception)
            {
                result = new(key, ToolState.Problem, "CheckFailed");
            }
            await gate.VerifyAsync(ct);
            failed |= result.State is ToolState.Problem or ToolState.NotConfigured;
            db.ToolCheckResults.Add(
                new()
                {
                    RequestId = request.Id,
                    Tool = result.Tool,
                    State = result.State,
                    Code = result.Code,
                    Detail = result.Detail.Truncate(1000),
                    WorkerId = runtime.Id,
                    DurationMs = watch.ElapsedMilliseconds,
                }
            );
            await db.SaveChangesAsync(ct);
        }
        request.Status = ToolRequestStatus.Completed;
        request.Outcome = failed ? "ProblemsFound" : "ChecksFinished";
        request.ActiveSlot = null;
        request.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
