using HiringPs.Core;
using Microsoft.Playwright;

/// <summary>Tests readable PeopleSoft details while the document remains loading and in approved frames.</summary>
internal static class InspiraContentChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        var detail = "<html><head><title>Applicant Home</title></head><body><h1>Programme Officer</h1><p>Job Opening ID: 287412</p><p>Duty Station: FO West Bank</p><h2>Responsibilities</h2><p>" + new string('x', 400) + "</p></body></html>";
        Check(!InspiraJobPage.HasJobDetails("Applicant Home Job Opening ID: search Duty Station: search Work Experience: search " + new string('x', 400)),
            "search filters cannot masquerade as a job detail document");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true,
            ExecutablePath = Environment.GetEnvironmentVariable("Browser__ExecutablePath") ?? "/usr/bin/chromium" });
        await using var context = await browser.NewContextAsync();
        var mode = "shell";
        await context.RouteAsync("**/*", async route =>
        {
            var html = route.Request.Url.Contains("foreign.test", StringComparison.Ordinal) ? detail : mode switch
            {
                "iframe" => "<html><body>Applicant Home<iframe srcdoc=\"" + System.Net.WebUtility.HtmlEncode(detail) + "\"></iframe></body></html>",
                "foreign" => "<html><body>Applicant Home<iframe src='https://foreign.test/job'></iframe></body></html>",
                _ => "<html><head><title>Applicant Home</title></head><body>Applicant Home</body></html>"
            };
            await route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Body = html });
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync("https://example.test/job?JobOpeningId=287412");
        await page.EvaluateAsync("html => { document.open(); document.write(html); }", detail);
        Check(await page.EvaluateAsync<string>("() => document.readyState") == "loading", "fixture reproduces the reported perpetual-loading document");
        var content = await InspiraJobPage.ReadAsync(page, ["example.test"], TimeSpan.FromSeconds(3), default);
        Check(content.Contains("287412") && content.Contains("Duty Station"), "readable job details are accepted without waiting for document.readyState");
        await page.EvaluateAsync("() => document.close()");
        mode = "iframe";
        await page.GotoAsync("https://example.test/job?JobOpeningId=287412");
        content = await InspiraJobPage.ReadAsync(page, ["example.test"], TimeSpan.FromSeconds(3), default);
        Check(content.Contains("Programme Officer") && !content.Contains("<iframe"), "same-origin embedded job details are extracted from their own document");
        mode = "foreign";
        await page.GotoAsync("https://example.test/job?JobOpeningId=287412");
        try { await InspiraJobPage.ReadAsync(page, ["example.test"], TimeSpan.FromMilliseconds(300), default); throw new Exception("Expected foreign frame rejection"); }
        catch (ContentRetryableException) { Check(true, "unapproved foreign frame cannot supply job details"); }
        mode = "shell";
        await page.GotoAsync("https://example.test/job?JobOpeningId=287412");
        try { await InspiraJobPage.ReadAsync(page, ["example.test"], TimeSpan.FromMilliseconds(300), default); throw new Exception("Expected portal-shell rejection"); }
        catch (ContentRetryableException ex) { Check(ex.Message.Contains("no source closure inferred"), "HTTP 200 applicant-home shell never becomes an extracted job or closure decision"); }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await InspiraJobPage.ReadAsync(page, ["example.test"], TimeSpan.FromSeconds(3), cancelled.Token); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { Check(true, "content polling respects collection cancellation"); }
        Console.WriteLine($"Inspira content: {passed} checks passed with local browser fixtures and no external requests.");
    }
}
