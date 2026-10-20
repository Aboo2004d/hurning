// Run against --membership-preview only: all data and mutation responses are synthetic.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');

(async () => {
    const base = new URL(process.env.HIRING_PREVIEW_URL || fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['127.0.0.1', 'localhost'].includes(base.hostname), 'Only the local preview server is allowed');
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH || undefined});
    const errors = [];
    let assertions = 0;
    const check = (value, name) => { assert.ok(value, name); assertions++; };
    async function context(role) {
        const result = await browser.newContext({extraHTTPHeaders: role ? {'X-Fixture-Role': role} : {}});
        // The preview must never contact an external identity, payment or publishing provider.
        await result.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
        result.on('page', page => page.on('pageerror', error => errors.push(error.message)));
        return result;
    }
    async function render(page, url, width) {
        await page.setViewportSize({width, height: 900});
        const response = await page.goto(new URL(url, base).href);
        check(response.status() === 200, `Render ${url}`);
        await page.locator('h1').first().waitFor();
        await page.evaluate(() => document.fonts.ready);
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `No page overflow: ${url} at ${width}`);
    }
    try {
        const anonymous = await context();
        const listing = await anonymous.newPage();
        for (const language of ['ar', 'en']) {
            for (const width of [360, 768, 1280]) {
                const query = new URLSearchParams({kind: 'Job', q: 'محاسب & مدقق', city: 'غزة', employment: 'دوام كامل'});
                await render(listing, `/${language}/ListingShellFixture?${query}`, width);
                check(await listing.locator('.opportunity-card').count() === 1, 'DTO-backed opportunity card is rendered');
                const feed = new URL(await listing.locator('a.feed-link').getAttribute('href'), base);
                check(feed.pathname === '/feeds/jobs.xml' && feed.searchParams.get('q') === 'محاسب & مدقق'
                    && feed.searchParams.get('city') === 'غزة' && feed.searchParams.get('employment') === 'دوام كامل', 'RSS follows current filters');
            }
        }
        await render(listing, '/ar/ListingShellFixture?closing=true', 360);
        check(await listing.locator('.closing-week').count() === 1, 'Closing-this-week is a separate section');
        check(await listing.locator('.closing-week').evaluate(section => Boolean(section.compareDocumentPosition(document.querySelector('.listing-layout')) & Node.DOCUMENT_POSITION_FOLLOWING)), 'Closing section precedes the results');
        check(await listing.locator('.opportunity-card .badge-new').count() === 1, 'New-item badge is preserved');
        fs.mkdirSync(path.join(__dirname, '../artifacts/quality'), {recursive: true});
        await listing.screenshot({path: path.join(__dirname, '../artifacts/quality/home-mobile.png'), fullPage: true});
        const member = await context('Member');
        const memberPage = await member.newPage();
        for (const language of ['ar', 'en']) {
            for (const width of [360, 1280]) {
                for (const action of ['', '/Preferences', '/Saved', '/Settings'])
                    await render(memberPage, `/${language}/Member/MemberShellFixture${action}`, width);
            }
        }
        const admin = await context('Admin');
        const adminPage = await admin.newPage();
        for (const language of ['ar', 'en']) {
            for (const width of [360, 1280]) {
                for (const screen of ['ai', 'messages', 'message', 'reports', 'merges', 'notifications', 'pages', 'item'])
                    await render(adminPage, `/${language}/Admin/AdminShellFixture/QualityPreview?screen=${screen}`, width);
            }
        }
        await render(adminPage, '/ar/Admin/AdminShellFixture/QualityPreview?screen=ai', 1280);
        check((await adminPage.locator('body').innerText()).includes('Daily AI token budget reached.'), 'AI pause reason is visible');
        check(await adminPage.locator('time[datetime]').count() === 1, 'AI pause resume time is visible');
        fs.mkdirSync(path.join(__dirname, '../artifacts/quality'), {recursive: true});
        await adminPage.screenshot({path: path.join(__dirname, '../artifacts/quality/ai-consumption.png'), fullPage: true});
        const toolsUrl = new URL('/ar/Admin/AdminShellFixture/Tools', base).href;
        await render(adminPage, toolsUrl, 360);
        const refreshed = (await adminPage.content()).replace('data-tools-dashboard', 'data-tools-dashboard data-fixture-refreshed="true"');
        let method;
        await adminPage.route('**/Admin/Tools/CheckTool?*', route => {
            method = route.request().method();
            return route.fulfill({status: 200, contentType: 'application/json', body: '{"queued":true}'});
        });
        await adminPage.route(toolsUrl, route => route.fulfill({status: 200, contentType: 'text/html', body: refreshed}));
        let navigations = 0;
        adminPage.on('framenavigated', frame => { if (frame === adminPage.mainFrame()) navigations++; });
        await adminPage.locator('[data-tool-check] button').first().click();
        await adminPage.locator('[data-fixture-refreshed]').waitFor();
        check(method === 'GET' && navigations === 0, 'Individual tool check uses fetch GET and updates without page navigation');
        check(await adminPage.locator('[data-tools-feedback]').isVisible(), 'Individual check feedback remains visible');
        check(errors.length === 0, 'No JavaScript page errors: ' + errors.join('; '));
        console.log(`PASS: ${assertions} browser checks across Arabic/English, mobile/desktop, RSS, DTO-backed screens and individual tool fetch.`);
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error.stack); process.exit(1); });
