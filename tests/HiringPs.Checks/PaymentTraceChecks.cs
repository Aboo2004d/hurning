using HiringPs.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public static class PaymentTraceChecks
{
    public static async Task RunAsync()
    {
        var logs = new List<string>();
        var writer = new PaymentTraceWriter(
            new BrokenScopeFactory(),
            new TraceHost(),
            new TraceLogger(logs)
        );
        var order = Guid.NewGuid();
        var trace = writer.Start(order);
        trace.Step("ProviderFailure", "AmountMismatch", 200);
        await trace.DisposeAsync();
        Check(
            logs.Any(x =>
                x.Contains("AmountMismatch") && x.Contains("200") && x.Contains(order.ToString())
            ),
            "Payment trace correlates order, stage, reason and HTTP status"
        );
        Check(
            logs.Any(x => x.Contains("StorageUnavailable"))
                && !logs.Any(x => x.Contains("DO_NOT_LOG_SECRET")),
            "Trace storage failure cannot break payment flow or reveal exception messages"
        );
        Check(
            writer.LastTraceId == trace.Id.ToString("N")
                && logs.Any(x => x.Contains(writer.LastTraceId!))
                && logs.Any(x => x.Contains(PaymentTraceWriter.BuildMarker)),
            "Trace exposes a support ID and identifies the executing code revision"
        );
        trace = writer.Start(order);
        trace.Persist = false;
        for (var i = 0; i < 20; i++)
            trace.Step("Provider", "Ready");
        await trace.DisposeAsync();
        Check(trace.Steps.Count == 12, "Persisted payment trace has a bounded step count");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class BrokenScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("DO_NOT_LOG_SECRET");
    }

    private sealed class TraceHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TraceCheck";
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TraceLogger(List<string> logs) : ILogger<PaymentTraceWriter>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel level) => true;

        public void Log<TState>(
            LogLevel level,
            EventId id,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => logs.Add(formatter(state, exception));
    }
}
