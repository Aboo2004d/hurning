// Local fixture pages only; blocks all external traffic and never submits a publish request.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['localhost', '127.0.0.1'].includes(base.hostname));
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH});
    let passed = 0;
    const check = (condition, message) => {assert.ok(condition, message); passed++;};
    fs.mkdirSync('artifacts/preference-telegram', {recursive: true});
    try {
        for (const language of ['ar', 'en']) for (const width of [360, 1280, 1536]) {
            const context = await browser.newContext({viewport: {width, height: 1000}, extraHTTPHeaders: {'X-Fixture-Role': 'Admin'}});
            await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const page = await context.newPage();
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            const prefix = `/${language}/Admin/AdminShellFixture/QualityPreview?screen=`;
            check((await page.goto(new URL(prefix + 'telegramcompose', base).href)).status() === 200, 'Telegram composer renders');
            await page.evaluate(() => document.fonts.ready);
            check(await page.locator('[name="Targets"][value="Jobs"]').isChecked(), 'Type channel is selected initially');
            check(!await page.locator('[name="Targets"][value="All"]').isChecked(), 'General channel is an optional additional destination');
            check(await page.locator('[name="Targets"]').count() === 2, 'Unrelated channels are absent');
            check((await page.locator('[data-social-compose] form').getAttribute('action')).includes('/TelegramCompose/Publish'), 'Form uses the protected publish action');
            check(await page.locator('[data-social-compose] [name="__RequestVerificationToken"]').count() === 1, 'Publish form has an antiforgery token');
            await page.locator('[name="Targets"][value="Jobs"]').uncheck();
            await page.locator('[data-social-compose] button[type="submit"]').click();
            await page.locator('[data-social-targets] [role="alert"]').waitFor();
            check(true, 'No selected destination blocks submission');
            await page.locator('[name="Targets"][value="All"]').check();
            check(await page.locator('[name="Targets"][value="All"]').isChecked(), 'General channel can be selected on its own');
            check(await page.locator('[data-social-targets] [role="alert"]').count() === 0, 'Selecting a destination clears the prior validation message');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Composer fits the viewport');
            if (language === 'ar' && width === 1280) await page.screenshot({path: 'artifacts/preference-telegram/compose.png', fullPage: true});
            await page.goto(new URL(prefix + 'planlimit', base).href);
            check(await page.locator('#MaxPreferredCategories').inputValue() === '3', 'Plan editor exposes the configured cap');
            await page.locator('#MaxPreferredCategories').fill('0');
            check(!await page.locator('#MaxPreferredCategories').evaluate(input => input.checkValidity()), 'Zero cap is invalid');
            await page.locator('#MaxPreferredCategories').fill('');
            check(await page.locator('#MaxPreferredCategories').evaluate(input => input.checkValidity()), 'Blank cap means unlimited');
            await context.close();
            const member = await browser.newContext({viewport: {width, height: 1000}, extraHTTPHeaders: {'X-Fixture-Role': 'Member'}});
            await member.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const preferences = await member.newPage();
            await preferences.goto(new URL(`/${language}/Member/MemberShellFixture/Preferences?categoryLimit=2`, base).href);
            check(await preferences.locator('[data-preference-quota]').getAttribute('data-selected') === '2'
                && await preferences.locator('[data-preference-quota]').getAttribute('data-limit') === '2', 'Selected count and subscription cap are displayed together');
            check((await preferences.locator('[data-preference-quota-status]').innerText()).trim().length > 0, 'Reaching the cap displays an explanation');
            check(await preferences.locator('button[name="addCategory"]').evaluateAll(buttons => buttons.length > 0 && buttons.every(x => x.disabled)), 'Add category is disabled at the plan cap');
            check(await preferences.locator('[value="select-categories"]').isDisabled(), 'Select all cannot bypass the cap');
            check(!await preferences.locator('[value="clear-categories"]').isDisabled(), 'Removing choices remains available');
            check(await preferences.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Limited preference editor fits the viewport');
            if (language === 'ar' && width === 1280) await preferences.screenshot({path: 'artifacts/preference-telegram/preferences.png', fullPage: true});
            await preferences.goto(new URL(`/${language}/Member/MemberShellFixture/Preferences?categoryLimit=3`, base).href);
            check(await preferences.locator('[data-preference-quota]').getAttribute('data-limit') === '3'
                && !await preferences.locator('button[name="addCategory"]').first().isDisabled(), 'Another choice remains available below the cap');
            await preferences.goto(new URL(`/${language}/Member/MemberShellFixture/Preferences`, base).href);
            check(!await preferences.locator('[value="select-categories"]').isDisabled(), 'Existing unlimited preferences keep select all');
            check(errors.length === 0, 'No composer JavaScript errors');
            await member.close();
        }
        console.log(`PASS: ${passed} preference quota and Telegram compose UI checks; no posts or payments sent.`);
    } finally {await browser.close();}
})().catch(error => {console.error(error.stack); process.exit(1);});
