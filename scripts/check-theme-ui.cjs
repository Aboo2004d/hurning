// Use --membership-preview; all external traffic is blocked, including Analytics.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(process.env.HIRING_PREVIEW_URL || fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['localhost', '127.0.0.1'].includes(base.hostname));
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH || undefined});
    let passed = 0;
    const check = (value, message) => {assert.ok(value, message); passed++;};
    const errors = [];
    async function context(options = {}) {
        const result = await browser.newContext({colorScheme: 'light', ...options});
        await result.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
        result.on('page', page => page.on('pageerror', error => errors.push(error.message)));
        return result;
    }
    async function theme(page, value) {
        await page.waitForFunction(expected => document.documentElement.dataset.theme === expected
            && getComputedStyle(document.documentElement).colorScheme === expected, value);
        check(true, 'Effective theme: ' + value);
    }
    async function openMenu(page) {
        if (await page.locator('.header-menu-toggle').isVisible()) await page.locator('.header-menu-toggle').click();
    }
    fs.mkdirSync(path.join(__dirname, '../artifacts/theme'), {recursive: true});
    try {
        for (const language of ['ar', 'en']) {
            for (const role of ['', 'Member', 'Admin']) {
                for (const width of [360, 1120, 1280]) {
                    const session = await context({viewport: {width, height: 900}, extraHTTPHeaders: role ? {'X-Fixture-Role': role} : {}});
                    const page = await session.newPage();
                    const response = await page.goto(new URL(`/${language}/ListingShellFixture`, base).href);
                    check(response.status() === 200, 'Public header renders for ' + role);
                    await theme(page, 'light');
                    check(await page.locator('[data-theme-toggle]').evaluate(button => button.previousElementSibling.matches('.language-toggle')
                        && (button.nextElementSibling.matches('.header-account') || button.nextElementSibling.querySelector('.header-account'))), 'Theme button is between language and account');
                    if (width === 360) check(!await page.locator('[data-theme-toggle]').isVisible(), 'Mobile button starts inside the closed menu');
                    await openMenu(page);
                    check(await page.locator('[data-theme-toggle]').isVisible(), 'Theme control is visible on desktop or in the open mobile menu');
                    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `No header overflow: ${language} ${role} ${width}`);
                    await page.locator('[data-theme-toggle]').click();
                    await theme(page, 'dark');
                    check(await page.locator('[data-theme-toggle]').getAttribute('aria-label') === (language === 'ar' ? 'الوضع النهاري' : 'Light mode'), 'Accessible action follows the selected theme');
                    await page.reload();
                    await theme(page, 'dark');
                    await openMenu(page);
                    await page.locator('[data-theme-toggle]').focus();
                    await page.keyboard.press('Space');
                    await theme(page, 'light');
                    if (language === 'ar' && role === 'Member' && width === 360)
                        await page.screenshot({path: path.join(__dirname, '../artifacts/theme/mobile-menu.png'), fullPage: false});
                    await session.close();
                }
            }
        }
        const session = await context();
        const page = await session.newPage();
        await page.goto(new URL('/ar/ListingShellFixture', base).href);
        await page.emulateMedia({colorScheme: 'dark'});
        await theme(page, 'dark');
        await page.locator('[data-theme-toggle]').click();
        await theme(page, 'light');
        await page.emulateMedia({colorScheme: 'light'});
        await page.emulateMedia({colorScheme: 'dark'});
        await theme(page, 'light');
        await page.goto(new URL('/en/ListingShellFixture', base).href);
        await theme(page, 'light');
        const other = await session.newPage();
        await other.goto(new URL('/ar/ListingShellFixture', base).href);
        await page.locator('[data-theme-toggle]').click();
        await theme(page, 'dark');
        await theme(other, 'dark');
        await session.close();
        for (const language of ['ar', 'en']) {
            for (const width of [360, 1100, 1120, 1280, 1440, 1920]) {
                const area = await context({viewport: {width, height: 900}, extraHTTPHeaders: {'X-Fixture-Role': 'Admin'}});
                const page = await area.newPage();
                await page.goto(new URL(`/${language}/Admin/AdminShellFixture/Tools`, base).href);
                await page.evaluate(() => document.fonts.ready);
                await openMenu(page);
                check(await page.evaluate(() => {
                    const actions = document.querySelector('.header-actions').getBoundingClientRect();
                    const overlap = [...document.querySelector('.admin-header-nav').children].some(child => {
                        const nav = child.getBoundingClientRect();
                        return Math.min(nav.right, actions.right) - Math.max(nav.left, actions.left) > 1
                            && Math.min(nav.bottom, actions.bottom) - Math.max(nav.top, actions.top) > 1;
                    });
                    return !overlap && document.documentElement.scrollWidth <= innerWidth + 1;
                }), `Admin navigation and actions never overlap: ${language} ${width}`);
                await area.close();
            }
        }
        for (const [role, url] of [['Admin', '/ar/Admin/AdminShellFixture/Tools'], ['Member', '/ar/Member/MemberShellFixture/Settings']]) {
            const area = await context({viewport: {width: 360, height: 900}, extraHTTPHeaders: {'X-Fixture-Role': role}});
            const page = await area.newPage();
            await page.goto(new URL(url, base).href);
            await openMenu(page);
            check(await page.locator('[data-theme-toggle]').isVisible(), 'Shared area mobile theme control: ' + role);
            await page.locator('[data-theme-toggle]').click();
            await theme(page, 'dark');
            await area.close();
        }
        const denied = await context();
        await denied.addInitScript(() => Object.defineProperty(window, 'localStorage', {get() {throw new Error('Storage unavailable');}}));
        const pageDenied = await denied.newPage();
        await pageDenied.goto(new URL('/ar/ListingShellFixture', base).href);
        await pageDenied.locator('[data-theme-toggle]').click();
        await theme(pageDenied, 'dark');
        await denied.close();
        const noScripts = await context({javaScriptEnabled: false, colorScheme: 'dark'});
        const fallback = await noScripts.newPage();
        await fallback.goto(new URL('/ar/ListingShellFixture', base).href);
        check(await fallback.locator('html').evaluate(root => getComputedStyle(root).colorScheme === 'dark')
            && !await fallback.locator('[data-theme-toggle]').isVisible(), 'System dark appearance remains available without JavaScript');
        await noScripts.close();
        check(errors.length === 0, 'No JavaScript errors: ' + errors.join('; '));
        console.log(`PASS: ${passed} theme header, mobile menu, persistence, keyboard and system preference checks.`);
    } finally {await browser.close();}
})().catch(error => {console.error(error.stack); process.exit(1);});
