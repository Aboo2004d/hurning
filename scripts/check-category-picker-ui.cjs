// Browser fixtures only. Blocks external traffic and never creates real opportunities or sends alerts.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const {chromium} = require(process.env.HIRING_PLAYWRIGHT_MODULE || 'playwright');
(async () => {
    const base = new URL(fs.readFileSync('/tmp/hiring-membership-shell-url', 'utf8').trim());
    assert.ok(['localhost', '127.0.0.1'].includes(base.hostname));
    const browser = await chromium.launch({headless: true, executablePath: process.env.HIRING_CHROMIUM_PATH});
    let passed = 0;
    const check = (value, label) => {assert.ok(value, label); passed++;};
    try {
        for (const language of ['ar', 'en']) for (const width of [360, 1280]) {
            const context = await browser.newContext({viewport: {width, height: 1000}, extraHTTPHeaders: {'X-Fixture-Role': 'Admin'}});
            await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const page = await context.newPage();
            const errors = []; page.on('pageerror', error => errors.push(error.message));
            check((await page.goto(new URL(`/${language}/Admin/AdminShellFixture/QualityPreview?screen=opportunitycategories`, base).href)).status() === 200, 'Opportunity editor renders');
            const input = page.locator('[data-category-picker] input');
            const results = page.locator('[data-category-picker] [role="listbox"]');
            await input.fill('هند');
            check((await results.locator('button').first().textContent()) === 'هندسة', 'Prefix matches ranked first');
            await input.fill('Programing');
            check((await results.locator('button').first().textContent()) === 'Programming', 'Nearby spelling suggested');
            check(await input.inputValue() === 'Programing', 'Suggestion does not silently rewrite input');
            await results.locator('button').first().click();
            check(await input.inputValue() === 'Programming', 'Explicit choice selects existing category');
            check(!await results.isVisible(), 'Selection closes suggestions');
            await input.fill('محاس'); await input.press('ArrowDown'); await input.press('Enter');
            check(await input.inputValue() === 'محاسبة', 'Keyboard can choose existing category');
            await input.fill('علوم البيانات');
            const newOption = results.locator('button').last();
            check((await newOption.getAttribute('data-category-value')) === 'علوم البيانات', 'New exact input remains selectable');
            await newOption.click();
            check(await input.inputValue() === 'علوم البيانات', 'New category selection preserves text');
            await input.fill('Design');
            check((await results.textContent()).includes('Design & UX <sample>'), 'Untrusted labels rendered as plain text');
            await input.press('Escape');
            check(!await results.isVisible(), 'Escape closes suggestions');
            await page.evaluate(() => document.fonts.ready);
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Editor fits viewport');
            await context.close();

            const member = await browser.newContext({viewport: {width, height: 1000}, extraHTTPHeaders: {'X-Fixture-Role': 'Member'}});
            await member.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const preferences = await member.newPage();
            const url = new URL(`/${language}/Member/MemberShellFixture/Preferences?categoryLimit=3`, base).href;
            check((await preferences.goto(url)).status() === 200, 'Preference editor renders capped account');
            check(await preferences.locator('[data-preference-quota]').getAttribute('data-limit') === '3', 'Three-category cap visible');
            const disclosure = preferences.locator('[data-preference-list="Categories"]');
            await disclosure.locator('summary').click();
            const search = disclosure.locator('[data-preference-category-search]');
            await search.fill('هند');
            check(await disclosure.locator('[data-preference-value]:visible').count() === 1, 'Member list filters as user types');
            check((await disclosure.locator('[data-preference-value]:visible').textContent()).includes('هندسة'), 'Matching category stays visible');
            await search.fill('علوم البيانات');
            const add = disclosure.locator('[data-preference-new-category]');
            check(await add.isVisible() && !await add.isDisabled() && await add.getAttribute('value') === 'علوم البيانات', 'New member category available below cap');
            await preferences.locator('[data-preferences-form]').evaluate((form, action) => { form.action = action; }, url);
            await add.click();
            await preferences.locator('[data-preference-quota][data-selected="3"]').waitFor();
            check(true, 'AJAX replacement retains updated quota');
            await preferences.locator('[data-preference-list="Categories"]').evaluate(element => { element.open = true; });
            await preferences.locator('[data-preference-category-search]').fill('مجال آخر');
            check(await preferences.locator('[data-preference-new-category]').isDisabled(), 'New category cannot bypass full quota');
            check(!await preferences.locator('[data-preference-selected="Categories"] button').first().isDisabled(), 'Removal remains possible at quota');
            check(!(await preferences.locator('[data-preference-editor]').innerText()).includes(language === 'ar' ? 'جميع المجالات' : 'all categories'), 'Empty categories are not advertised as wildcard');
            check(await preferences.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Preferences fit viewport');
            check(errors.length === 0, 'No picker JavaScript errors');
            await member.close();
        }
        console.log(`Category picker: ${passed} browser checks passed.`);
    } finally {await browser.close();}
})().catch(error => {console.error(error); process.exitCode = 1;});
