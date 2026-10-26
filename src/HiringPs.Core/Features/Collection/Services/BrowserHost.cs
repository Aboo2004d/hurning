// متصفح Chromium واحد مشترك للعامل: يُشغَّل عند أول حاجة ويُعاد تشغيله إذا انقطع، وكل عنصر يأخذ سياقًا مستقلًا.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>مكون BrowserHost ضمن مجال Collection؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed class BrowserHost(IConfiguration configuration, ILogger<BrowserHost>? logger = null)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;
    private bool disposed;

    /// <summary>يعيد متصفح Chromium المشترك ويفتحه عند الحاجة تحت قفل المالك.</summary>
    public async Task<IBrowser> GetAsync(CancellationToken ct)
    {
        if (browser is { IsConnected: true } ready)
            return ready;
        await gate.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (browser is { IsConnected: true } current)
                return current;
            if (browser is not null)
            {
                logger?.LogWarning("Chromium disconnected; launching a new browser.");
                await CloseBrowserAsync();
            }
            try
            {
                playwright ??= await BrowserSettings.CreateAsync(configuration);
                browser = await playwright.Chromium.LaunchAsync(
                    new()
                    {
                        Headless = true,
                        ExecutablePath = BrowserSettings.ExecutablePath(configuration),
                        Channel = configuration["Browser:Channel"] is { Length: > 0 } channel
                            ? channel
                            : null,
                        Args = BrowserSettings.Args(configuration),
                    }
                );
            }
            catch
            {
                // A dead driver cannot launch again; recreate it on the next request.
                playwright?.Dispose();
                playwright = null;
                throw;
            }
            return browser;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>يغلق نسخة المتصفح الحالية دون ترك مورد متصل معطل.</summary>
    private async Task CloseBrowserAsync()
    {
        try
        {
            if (browser is not null)
                await browser.DisposeAsync();
        }
        catch (Exception ex)
        {
            // Closing an already crashed browser is best effort.
            logger?.LogDebug("Closing Chromium failed ({Type}).", ex.GetType().Name);
        }
        browser = null;
    }

    /// <summary>يغلق الموارد التي يملكها المكون مع احترام ملكية المتصفح المشتركة.</summary>
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed)
                return;
            disposed = true;
            await CloseBrowserAsync();
            playwright?.Dispose();
            playwright = null;
        }
        finally
        {
            gate.Release();
        }
    }
}
