using Microsoft.Playwright;

internal static class PreferenceSettingsUiChecks
{
    public static Task RunAsync() => MembershipChecks.ShellAsync(inspect: InspectAsync);
    private static async Task InspectAsync(string origin)
    {
        var checks = 0;
        void Check(bool valid, string label) { if (!valid) throw new Exception(label); checks++; }
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        { Headless = true, ExecutablePath = Environment.GetEnvironmentVariable("Browser__ExecutablePath") });
        Directory.CreateDirectory("/tmp/hiring-preference-ui");
        foreach (var theme in new[] { "light", "dark" })
        {
            await using var context = await browser.NewContextAsync(new() { ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Fixture-Role"] = "Admin" } });
            await context.AddInitScriptAsync("localStorage.setItem('hiring.theme.v1', '" + theme + "')");
            await context.RouteAsync("**/*", route => new Uri(route.Request.Url).Host == "127.0.0.1" ? route.ContinueAsync() : route.AbortAsync());
            var page = await context.NewPageAsync();
            foreach (var language in new[] { "ar", "en" }) foreach (var width in new[] { 1280, 390 })
            {
                await page.SetViewportSizeAsync(width, 1000);
                foreach (var view in new[] { "PreferenceSettings", "MemberMessageTools", "MemberMessageCompose", "MemberMessagePreview", "MemberPreferences" })
                {
                    var response = await page.GotoAsync(origin + "/" + language + "/Admin/AdminShellFixture/" + view);
                    Check(response?.Ok == true, "Actual administrator view renders: " + view);
                    Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"),
                        "No horizontal overflow: " + view + " " + language + " " + theme + " " + width);
                    Check(await page.Locator("main .admin-action").CountAsync() > 0, "New actions use the shared administrator button style");
                    Check(await page.Locator("main .admin-action").EvaluateAllAsync<bool>("elements => elements.every(el => ['inline-flex', 'flex'].includes(getComputedStyle(el).display) && parseFloat(getComputedStyle(el).minHeight) >= 44)"),
                        "Action controls retain accessible dimensions and icon/text layout");
                    if (view == "PreferenceSettings")
                    {
                        Check(await page.Locator("input[name=NonSubscriberCategoryLimit]").InputValueAsync() == "2",
                            "Administrator quota is shown in the number field");
                        var primary = page.Locator("form button[type=submit].admin-action");
                        var accent = await primary.EvaluateAsync<string>("el => { const probe = document.createElement('span'); probe.style.color = 'var(--accent)'; el.append(probe); const color = getComputedStyle(probe).color; probe.remove(); return color; }");
                        // The theme applies with the site's color transition; wait for its final rendered color.
                        await Assertions.Expect(primary).ToHaveCSSAsync("background-color", accent);
                        checks++;
                        if (language == "ar") await page.ScreenshotAsync(new() { Path = $"/tmp/hiring-preference-ui/quota-{theme}-{width}.png", FullPage = true });
                    }
                    if (view == "MemberMessagePreview")
                        Check(await page.Locator("main form button[type=submit]").IsDisabledAsync(), "Empty broadcast preview keeps sending disabled");
                }
            }
        }
        Console.WriteLine($"Preference settings UI: {checks} browser checks passed; desktop/mobile, Arabic/English, light/dark, no database or Telegram calls.");
    }
}
