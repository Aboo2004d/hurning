using Microsoft.Playwright;

/// <summary>Exercises real browser form behavior with no network or platform connections.</summary>
internal static class RandomPublishingUiChecks
{
    public static async Task RunAsync()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        { Headless = true, ExecutablePath = Environment.GetEnvironmentVariable("Browser__ExecutablePath") });
        var page = await browser.NewPageAsync();
        string Form(string id) => $"""
            <form id="{id}" data-random-schedule data-slot-label="وقت نشر المنشور">
            <input data-random-count value="2" type="number" min="1" max="24">
            <div data-random-times><label><input name="Times[0]" type="time" value="09:00"></label>
            <label><input name="Times[1]" type="time" value="09:05"></label></div></form>
            """;
        await page.SetContentAsync(Form("facebook") + Form("linkedin"));
        await page.AddScriptTagAsync(new() { Path = Path.GetFullPath("src/HiringPs.Web/wwwroot/js/random-publishing.js") });
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
        await page.Locator("#facebook [data-random-count]").FillAsync("3");
        Check(await page.Locator("#facebook [data-random-times] input").CountAsync() == 3, "increasing count creates one time field per post");
        Check(await page.Locator("#facebook input[name='Times[2]']").InputValueAsync() == "09:10", "new post defaults five minutes after previous post");
        Check(await page.Locator("#linkedin [data-random-times] input").CountAsync() == 2, "changing Facebook count does not change LinkedIn schedule");
        await page.Locator("#facebook input[name='Times[0]']").FillAsync("08:45");
        await page.Locator("#facebook [data-random-count]").FillAsync("4");
        Check(await page.Locator("#facebook input[name='Times[0]']").InputValueAsync() == "08:45", "edited times survive count changes");
        await page.Locator("#facebook [data-random-count]").FillAsync("1");
        Check(await page.Locator("#facebook [data-random-times] input").CountAsync() == 1, "reducing count removes extra posted fields");
        await page.Locator("#facebook [data-random-count]").FillAsync("4");
        Check(await page.Locator("#facebook input[name='Times[3]']").InputValueAsync() == "09:15", "restoring count preserves existing slot times");
        await page.Locator("#facebook [data-random-count]").FillAsync("25");
        Check(await page.Locator("#facebook [data-random-times] input").CountAsync() == 4, "out-of-range count does not create unbounded fields");
        Console.WriteLine("Random publishing UI: 7 browser checks passed without network.");
    }
}
