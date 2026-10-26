using HiringPs.Core;
using Microsoft.Extensions.Configuration;

internal static class BrowserDriverChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string description)
        {
            if (!value) throw new Exception(description);
            count++;
            Console.WriteLine("PASS " + description);
        }
        var actual = BrowserDriverDiagnostics.Paths();
        BrowserDriverDiagnostics.ValidateBundle(actual);
        var folder = Path.Combine(Path.GetTempPath(), "hiring-driver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var oldSearch = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var oldNode = Environment.GetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH");
        try
        {
            var cli = Path.Combine(folder, ".playwright", "package", "cli.js");
            Directory.CreateDirectory(Path.GetDirectoryName(cli)!);
            await File.WriteAllTextAsync(cli, "process.stderr.write('fixture driver startup refused'); process.exit(23);");
            var fixture = new BrowserDriverPathsDto(actual.Node, cli);
            var probe = await BrowserDriverDiagnostics.ProbeAsync(fixture);
            Check(probe.Contains("ExitCode=23") && probe.Contains("fixture driver startup refused")
                && probe.StartsWith("Diagnostic driver probe:"), "driver probe captures actual stderr and exit code as a separate diagnostic attempt");
            var missing = await BrowserDriverDiagnostics.ProbeAsync(new(Path.Combine(folder, "missing-node.exe"), cli));
            Check(missing.Contains("missing") && missing.Contains("FileNotFoundException"), "missing deployed node is identified before launching");

            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", folder);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", actual.Node);
            var config = new ConfigurationBuilder().Build();
            try
            {
                using var runtime = await BrowserSettings.CreateAsync(config).WaitAsync(TimeSpan.FromSeconds(10));
                throw new Exception("Expected driver startup failure");
            }
            catch (BrowserRuntimeException ex)
            {
                Check(ex.Stage == BrowserFailureStage.DriverStartup && ex.Message.Contains("ExitCode=23")
                    && ex.Message.Contains("fixture driver startup refused") && ex.Message.Contains("Node="),
                    "real Playwright driver termination is diagnosed before Edge launch");
            }
            var result = await new ToolProbes(config).CheckAsync("Chromium", default);
            Check(result.Code == "BrowserDriverFailed" && result.Detail.Contains("DriverStartup") && result.Detail.Contains("ExitCode=23"),
                "tool result keeps startup stage and native exit code for operation history");
            File.Delete(cli);
            result = await new ToolProbes(config).CheckAsync("Chromium", default);
            Check(result.Code == "BrowserDriverFailed" && result.Detail.Contains("CLI is missing"),
                "incomplete Playwright package cannot be misreported as Edge launch failure");

            await File.WriteAllTextAsync(cli, "setInterval(() => {}, 1000);");
            var alive = await BrowserDriverDiagnostics.ProbeAsync(fixture);
            Check(alive.Contains("survived startup") && alive.Contains("not reproduced"),
                "probe surviving startup does not invent a cause or an original process exit code");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try { await BrowserDriverDiagnostics.ProbeAsync(fixture, cancelled.Token); throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException) { Check(true, "shutdown cancellation is preserved"); }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", oldSearch);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", oldNode);
            Directory.Delete(folder, true);
        }
        Console.WriteLine($"Browser driver: {count} checks passed; no browser or remote request was launched by the diagnostic probe.");
    }
}
