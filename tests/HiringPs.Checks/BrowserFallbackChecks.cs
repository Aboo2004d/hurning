using System.Reflection;
using HiringPs.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

internal static class BrowserFallbackChecks
{
    public static async Task RunAsync()
    {
        foreach (var failures in new[] { 0, 1, 2 })
        {
            var channels = new List<string?>();
            var expected = Proxy<IBrowser>((_, _) => throw new NotSupportedException());
            var browserType = Proxy<IBrowserType>((method, args) =>
            {
                if (method.Name != "LaunchAsync") throw new NotSupportedException();
                var options = (BrowserTypeLaunchOptions)args![0]!;
                channels.Add(options.Channel);
                if (channels.Count <= failures)
                    return Task.FromException<IBrowser>(new PlaywrightException("launch " + channels.Count));
                return Task.FromResult(expected);
            });
            var runtime = Proxy<IPlaywright>((method, _) =>
                method.Name == "get_Chromium" ? browserType : throw new NotSupportedException());
            try
            {
                var result = await BrowserSettings.LaunchWithDetailsAsync(runtime, new ConfigurationBuilder().Build());
                if (failures == 2 || result.Browser != expected) throw new Exception("Incorrect browser launch result");
                if (result.Name != (failures == 0 ? "Microsoft Edge" : "Chromium") || result.UsedFallback != (failures == 1))
                    throw new Exception("Browser identity must match the successful launch attempt");
            }
            catch (PlaywrightException ex) when (failures == 2)
            {
                if (!ex.Message.Contains("launch 1") || !ex.Message.Contains("launch 2"))
                    throw new Exception("Both launch failures must remain diagnosable");
            }
            if (channels[0] != "msedge" || channels.Count != (failures == 0 ? 1 : 2)
                || (channels.Count == 2 && channels[1] is not null))
                throw new Exception("Edge must precede bundled Chromium, with at most two attempts");
        }
        Console.WriteLine("PASS Edge preference, Chromium fallback, and combined launch failures");
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, BrowserProxy>();
        ((BrowserProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}

public class BrowserProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
}
