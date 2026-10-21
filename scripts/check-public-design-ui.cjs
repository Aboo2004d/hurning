// Render the approved public design using --membership-preview and synthetic data only.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(process.env.HIRING_PREVIEW_URL || fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['127.0.0.1', 'localhost'].includes(base.hostname));
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH || undefined});
    let count = 0;
    const check = (condition, message) => {assert.ok(condition, message); count++;};
    const errors = [];
    fs.mkdirSync(path.join(__dirname, '../artifacts/public-design'), {recursive: true});
    try {
        for (const colorScheme of ['light', 'dark']) {
            const context = await browser.newContext({colorScheme});
            await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const page = await context.newPage();
            page.on('pageerror', error => errors.push(error.message));
            for (const language of ['ar', 'en']) {
                for (const width of [360, 768, 1280]) {
                    await page.setViewportSize({width, height: 900});
                    for (const screen of ['Overview', '?q=accountant&city=غزة&closing=true', 'Details']) {
                        const url = `/${language}/ListingShellFixture${screen.startsWith('?') ? screen : '/' + screen}`;
                        const response = await page.goto(new URL(url, base).href);
                        check(response.status() === 200, `Render ${url}`);
                        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `No page overflow: ${screen} ${colorScheme} ${language} ${width}`);
                        if (screen === 'Overview') {
                            check(await page.locator('.hero-stats').count() === 1 && await page.locator('.kind-tabs a').count() === 5, 'Approved hero, statistics and kind tabs remain present');
                            check(await page.locator('.org-avatar').first().textContent() === 'م', 'Approved organization initial remains present');
                            if (language === 'ar' && width === 1280 && colorScheme === 'light')
                                await page.screenshot({path: path.join(__dirname, '../artifacts/public-design/home.png'), fullPage: true});
                        } else if (screen === 'Details') {
                            check((await page.locator('body').textContent()).includes('Verified fixture evidence'), 'Verified date evidence renders from the real details DTO');
                            check(await page.locator('h1 bdi[dir="ltr"]').textContent() === '(GS Support Intern)', 'Mixed-language title keeps its isolated parentheses');
                            check(await page.locator('h1 script, h1 img').count() === 0 && (await page.locator('h1').textContent()).includes('<اختبار>'), 'Title markup remains encoded');
                            if (language === 'ar' && width === 360 && colorScheme === 'dark')
                                await page.screenshot({path: path.join(__dirname, '../artifacts/public-design/details-dark-mobile.png'), fullPage: true});
                        } else {
                            check(await page.locator('.closing-week').count() === 1, 'Approved closing-week section remains independent');
                            check(await page.locator('#listing-query').evaluate(input => input.form?.id === 'listing-form'
                                && new FormData(input.form).getAll('q').length === 1 && new FormData(input.form).get('q') === 'accountant'), 'Hero search participates in the existing filter form exactly once');
                        }
                    }
                }
            }
            await page.goto(new URL('/ar/ListingShellFixture?restricted=true', base).href);
            check(await page.locator('.opportunity-card a.card-title-link').count() === 0
                && await page.locator('.subscriber-details-hint a').count() === 1, 'Restricted opportunities retain their subscription gate');
            await page.goto(new URL('/ar/ListingShellFixture', base).href);
            await page.route('**/Opportunities/Details/**', route => route.fulfill({status: 200, contentType: 'text/html', body: '<h1>Card destination</h1>'}));
            await page.locator('.opportunity-card .summary').scrollIntoViewIfNeeded();
            const box = await page.locator('.opportunity-card .summary').boundingBox();
            await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
            await page.waitForURL('**/Opportunities/Details/**');
            check(page.url().includes('/Opportunities/Details/1'), 'The approved stretched card link remains clickable');
            await context.close();
        }
        check(errors.length === 0, 'No JavaScript errors: ' + errors.join('; '));
        console.log(`PASS: ${count} approved public design browser checks.`);
    } finally {await browser.close();}
})().catch(error => {console.error(error.stack); process.exit(1);});
