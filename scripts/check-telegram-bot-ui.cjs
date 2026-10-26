// Synthetic local preview only. Never calls Telegram or changes a real webhook.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['127.0.0.1', 'localhost'].includes(base.hostname));
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH});
    let checks = 0;
    const check = (ok, label) => { assert.ok(ok, label); checks++; };
    try {
        const context = await browser.newContext({extraHTTPHeaders: {'X-Fixture-Role': 'Admin'}});
        await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        for (const language of ['ar', 'en']) for (const width of [360, 768, 1280]) {
            await page.setViewportSize({width, height: 900});
            const response = await page.goto(new URL(`/${language}/Admin/AdminShellFixture/TelegramBot`, base).href);
            check(response.status() === 200, 'admin setup view renders');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'setup fits viewport');
            const card = page.locator('#telegram-bot-setup');
            check((await card.textContent()).includes('https://hiring.example/integrations/telegram'), 'expected callback is visible');
            check((await card.textContent()).includes('https://old-tunnel.example/integrations/telegram'), 'previous callback is visible');
            const form = card.locator('form');
            check((await form.getAttribute('method')).toLowerCase() === 'post'
                && (await form.getAttribute('action')).endsWith('/Admin/TelegramBot/Activate'), 'activation points to protected POST action');
            check(await form.locator('input[name="__RequestVerificationToken"]').count() === 1, 'activation has antiforgery token');
            check(await card.locator('button[type="submit"]').isEnabled(), 'valid configuration enables explicit activation');
            check(!(await card.textContent()).includes('TelegramBotState_'), 'state is translated');
        }
        await page.goto(new URL('/ar/Admin/AdminShellFixture/TelegramBot?state=Disabled', base).href);
        check(await page.locator('#telegram-bot-setup button[type="submit"]').isDisabled(), 'disabled setup prevents activation');
        check(errors.length === 0, errors.join('\n'));
        await context.close();
        console.log(`Telegram bot setup UI: ${checks} checks passed; no provider requests or registration performed.`);
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
