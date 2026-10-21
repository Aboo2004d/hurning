// Renders local fixtures only; all external requests are blocked.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['localhost', '127.0.0.1'].includes(base.hostname));
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH});
    let passed = 0;
    const check = (value, text) => { assert.ok(value, text); passed++; };
    try {
        for (const language of ['ar', 'en']) for (const width of [360, 1280]) {
            const context = await browser.newContext({viewport: {width, height: 1000}, extraHTTPHeaders: {'X-Fixture-Role': 'Admin'}});
            await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const page = await context.newPage();
            const dialogs = [];
            page.on('dialog', dialog => { dialogs.push(dialog.message()); dialog.dismiss(); });
            for (const screen of ['systemerrors', 'systemerrordetails']) {
                const response = await page.goto(new URL(`/${language}/Admin/AdminShellFixture/QualityPreview?screen=${screen}&Search=trace-probe`, base).href);
                check(response.status() === 200, `${screen} renders`);
                await page.evaluate(() => document.fonts.ready);
                check(await page.locator('h1').count() === 1, 'Single page heading');
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'No horizontal overflow');
                check(await page.locator('.system-errors').innerText().then(text => text.includes(language === 'ar' ? 'توقيت فلسطين' : 'Palestine')), 'Palestine time label');
                if (screen === 'systemerrors') {
                    check(await page.locator('input[name="Search"]').inputValue() === 'trace-probe', 'Search retained');
                    const href = await page.locator('.pagination a').first().getAttribute('href');
                    check(href.includes('Search=trace-probe') && href.includes('page='), 'Pagination retains filters');
                    check((await page.locator('.system-errors article').innerText()).includes('<script>alert(1)</script>'), 'Error messages escaped');
                    const sequence = await page.locator('a[href*="/Operations"], a[href*="/SystemErrors"]').evaluateAll(nodes => nodes.map(n => n.getAttribute('href')));
                    check(sequence.some((link, i) => link.includes('/Operations') && sequence[i + 1]?.includes('/SystemErrors')), 'Navigation directly follows operation history');
                } else {
                    check((await page.locator('.system-error-stack').textContent()).includes('<script>alert(1)</script>'), 'Stack traces escaped');
                }
                check(dialogs.length === 0, 'Diagnostic content cannot execute scripts');
            }
            await context.close();
        }
        console.log(`System error UI: ${passed} checks passed.`);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
