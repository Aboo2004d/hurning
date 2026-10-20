// Use only --membership-preview; no real provider or database is contacted.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(process.env.HIRING_PREVIEW_URL || fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['127.0.0.1', 'localhost'].includes(base.hostname));
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH || undefined});
    const context = await browser.newContext({extraHTTPHeaders: {'X-Fixture-Role': 'Admin'}});
    await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    let passed = 0;
    const check = (value, text) => {assert.ok(value, text); passed++;};
    try {
        for (const language of ['ar', 'en']) {
            for (const width of [360, 1280]) {
                await page.setViewportSize({width, height: 900});
                for (const screen of ['operations', 'telegramtemplates', 'telegrambot', 'telegramchannel']) {
                    const result = await page.goto(new URL(`/${language}/Admin/AdminShellFixture/QualityPreview?screen=${screen}`, base).href);
                    check(result.status() === 200, `Render ${screen}`);
                    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `No overflow: ${screen} ${language} ${width}`);
                    if (screen === 'operations') {
                        check(await page.locator('.operation-event').count() === 1, 'Operation event is shown');
                        check(await page.locator('time[datetime]').count() === 1 && (await page.locator('time').textContent()).includes(language === 'ar' ? 'توقيت فلسطين' : 'Palestine'), 'Event time identifies Palestine timezone');
                        const next = new URL(await page.locator('.pagination a').last().getAttribute('href'), base);
                        check(next.searchParams.get('screen') === 'operations', 'Pagination retains current query');
                        check(await page.locator('select[name="Area"]').inputValue() === 'Collection' && await page.locator('select[name="Outcome"]').inputValue() === 'Failed', 'Selected filters remain visible');
                    }
                    if (screen === 'telegramtemplates') {
                        check(await page.locator('select[name="provider"] option[value="Telegram"]').count() === 1
                            && await page.locator('select[name="provider"] option[value="TelegramBot"]').count() === 1, 'Separate channel and bot editors are available');
                    }
                    if (screen === 'telegrambot') {
                        check(await page.locator('input[name="Provider"]').inputValue() === 'TelegramBot', 'Editor preserves independent bot provider');
                        await page.locator('[data-template-body]').fill('{{title}}');
                        await page.locator('[data-template-token="summary"]').click();
                        check((await page.locator('[data-template-body]').inputValue()).includes('{{summary}}'), 'Token insertion works');
                    }
                }
            }
        }
        await page.goto(new URL('/ar/Admin/AdminShellFixture/QualityPreview?screen=operations&Area=Collection&Outcome=Failed&Search=Source', base).href);
        const next = new URL(await page.locator('.pagination a').last().getAttribute('href'), base);
        check(next.searchParams.get('Area') === 'Collection' && next.searchParams.get('Outcome') === 'Failed'
            && next.searchParams.get('Search') === 'Source', 'Operation pagination preserves active filters');
        await page.setViewportSize({width: 1280, height: 900});
        fs.mkdirSync(path.join(__dirname, '../artifacts/operations'), {recursive: true});
        await page.screenshot({path: path.join(__dirname, '../artifacts/operations/history.png'), fullPage: true});
        for (const single of [false, true]) {
            await page.goto(new URL(`/ar/Admin/AdminShellFixture/Tools?checkedTools=true&single=${single}`, base).href);
            check(await page.locator('[data-tool] .tool-ready').count() === 15, 'All per-tool results stay ready across different worker heartbeats');
        }
        const toolsUrl = page.url();
        const refreshed = await page.content();
        let method;
        await page.route('**/Admin/Tools/CheckTool?*', route => {
            method = route.request().method();
            return route.fulfill({status: 200, contentType: 'application/json', body: '{"queued":true}'});
        });
        await page.route(toolsUrl, route => route.fulfill({status: 200, contentType: 'text/html', body: refreshed.replace('data-tools-dashboard', 'data-tools-dashboard data-refresh-complete="true"')}));
        await page.locator('[data-tool="Chromium"] button').click();
        await page.locator('[data-refresh-complete]').waitFor();
        check(method === 'GET' && await page.locator('[data-tool] .tool-ready').count() === 15, 'Individual fetch keeps every other verified card ready after refresh');
        check(errors.length === 0, 'No JavaScript errors: ' + errors.join('; '));
        console.log(`PASS: ${passed} operations, Telegram templates and tool state browser checks.`);
    } finally {await browser.close();}
})().catch(error => {console.error(error.stack); process.exit(1);});
