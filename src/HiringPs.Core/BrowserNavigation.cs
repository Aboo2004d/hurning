using Microsoft.Playwright;

namespace HiringPs.Core;

public static class BrowserNavigation
{
    // Wait for the site's own navigation only; never solve challenges or reload repeatedly.
    public static async Task OpenAsync(IPage page, string url, WaitUntilState ready,
        int forbiddenWaitSeconds, CancellationToken ct)
    {
        IResponse? latest = null;
        void Observe(object? sender, IResponse response)
        {
            if (response.Request.IsNavigationRequest && response.Frame == page.MainFrame) latest = response;
        }
        page.Response += Observe;
        try
        {
            ct.ThrowIfCancellationRequested();
            var initial = await page.GotoAsync(url, new() { WaitUntil = ready, Timeout = 45000 });
            latest ??= initial;
            if (initial?.Status == 403)
            {
                var duration = TimeSpan.FromSeconds(Math.Clamp(forbiddenWaitSeconds, 0, 30));
                var deadline = DateTimeOffset.UtcNow + duration;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    ct.ThrowIfCancellationRequested();
                    if (latest is { Status: >= 200 and < 300 } && !page.IsClosed)
                    {
                        try
                        {
                            var usable = await page.EvaluateAsync<bool>("() => document.readyState !== 'loading' && document.body && document.body.innerText.trim().length >= 80 && !/just a moment|access denied|request blocked|verify you are human|403 error/i.test(document.title + ' ' + document.body.innerText.slice(0,500))");
                            if (usable) return;
                        }
                        catch (PlaywrightException) when (!page.IsClosed) { /* Automatic navigation may replace the document. */ }
                    }
                    await Task.Delay(500, ct);
                }
                throw new InvalidOperationException($"Source returned HTTP 403; access still blocked after waiting {duration.TotalSeconds:0} seconds. Collection stopped; not evidence of closure.");
            }
            if (initial is null || initial.Status >= 400)
                throw new InvalidOperationException($"Source returned HTTP {initial?.Status}; collection stopped; not evidence of closure.");
        }
        finally { page.Response -= Observe; }
    }
}
