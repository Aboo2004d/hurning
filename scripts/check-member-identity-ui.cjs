const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['127.0.0.1', 'localhost'].includes(base.hostname));
    const browser = await chromium.launch({ headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH });
    let passed = 0;
    const check = (value, message) => { assert.ok(value, message); passed++; };
    try {
        for (const language of ['ar', 'en']) for (const width of [360, 1280]) {
            const context = await browser.newContext({ viewport: { width, height: 1000 }, extraHTTPHeaders: { 'X-Fixture-Role': 'Admin' } });
            await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const page = await context.newPage();
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            check((await page.goto(new URL(`/${language}/Admin/AdminFormShellFixture/ListPreview?kind=Subscriptions`, base).href)).status() === 200, 'Subscriptions render');
            check(await page.locator('tbody a').first().innerText() === 'عضو تجريبي', 'Subscription displays name instead of internal ID');
            const grant = page.locator('form[ action*="PreviewGrant"]');
            check(await grant.locator('input[name="Email"][type="email"]').count() === 1, 'Grant asks for email');
            check(await grant.locator('input[name="userId"]').count() === 0, 'Grant does not ask for a user ID');
            check(await grant.locator('input[name="__RequestVerificationToken"]').count() === 1, 'Preview uses CSRF protection');
            check((await grant.getAttribute('action')).includes('/Subscriptions/PreviewGrant'), 'Grant previews account before mutation');
            await page.goto(new URL(`/${language}/Admin/AdminShellFixture/QualityPreview?screen=subscriptionsconfirm`, base).href);
            check((await page.locator('main').innerText()).includes('عضو تجريبي'), 'Confirmation shows member name');
            check((await page.locator('main').innerText()).includes('member@example.test'), 'Confirmation shows exact email');
            check(!(await page.locator('main').innerText()).includes('fixture-internal-id'), 'Internal ID is hidden from confirmation text');
            const confirm = page.locator('form[action*="/Subscriptions/Grant"]');
            check(await confirm.locator('[name="Email"]').inputValue() === 'member@example.test', 'Confirmed email preserved');
            check(await confirm.locator('[name="__RequestVerificationToken"]').count() === 1, 'Final grant uses CSRF protection');
            await page.goto(new URL(`/${language}/Admin/AdminFormShellFixture/ListPreview?kind=Notifications`, base).href);
            check(await page.locator('input[name="email"][type="email"]').count() === 1, 'Notification filter uses email');
            check(await page.locator('input[name="userId"]').count() === 0, 'Notification filter has no ID input');
            check(await page.locator('tbody a').first().innerText() === 'عضو تجريبي', 'Notification list displays member name');
            check(errors.length === 0, 'No browser errors');
            await context.close();
        }
        console.log(`Member identity UI: ${passed} checks passed.`);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
