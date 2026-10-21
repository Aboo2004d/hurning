// Local Razor fixtures only; no approval reaches SQL and all external traffic is blocked.
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
            const context = await browser.newContext({viewport: {width, height: 1100}, extraHTTPHeaders: {'X-Fixture-Role': 'Admin'}});
            await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const page = await context.newPage(); const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            const prefix = `/${language}/Admin/AdminShellFixture/QualityPreview?screen=`;
            check((await page.goto(new URL(prefix + 'categorygroups&search=education&groupPage=2&categoryPage=2', base).href)).status() === 200, 'Group index renders');
            await page.evaluate(() => document.fonts.ready);
            check(await page.locator('h1').count() === 1, 'Single heading');
            check(await page.locator('#ungrouped-categories article').count() === 1, 'Unassigned names stay visible separately');
            check(await page.locator('#approved-groups article').count() === 1, 'Approved groups displayed separately');
            check((await page.locator('#ungrouped-categories a[href*="/Edit/"]').getAttribute('href')).includes('add=educatoin'), 'Joining proposal opens editor with source name');
            const create = await page.locator('#ungrouped-categories a[href*="second="]').getAttribute('href');
            check(create.includes('first=educatoin') && create.includes('second=educaton'), 'New proposal is reviewable before approval');
            const pendingLink = await page.locator('#ungrouped-categories .pagination a').first().getAttribute('href');
            check(pendingLink.includes('categoryPage=1') && pendingLink.includes('groupPage=2') && pendingLink.includes('search=education'), 'Pending pagination preserves other page and search');
            const groupLink = await page.locator('#approved-groups .pagination a').first().getAttribute('href');
            check(groupLink.includes('groupPage=1') && groupLink.includes('categoryPage=2'), 'Group pagination remains independent');
            check((await page.locator('#approved-groups time').innerText()).includes(language === 'ar' ? 'توقيت فلسطين' : 'Palestine'), 'Palestine time displayed');
            check(await page.locator('a[href*="/CategoryGroups"]').count() > 0, 'Admin navigation exposes group page');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Index fits viewport');
            await page.goto(new URL(prefix + 'categorygroupeditor', base).href);
            const form = page.locator('[data-category-group-editor]');
            check((await form.getAttribute('action')).includes('/CategoryGroups/Save'), 'Approval posts to correct controller');
            check(await form.locator('[name="__RequestVerificationToken"]').count() === 1, 'Approval protected by antiforgery token');
            check(await form.locator('[name="Members"][value="education"]').isChecked(), 'Approved members preselected');
            check(await form.locator('[name="Members"][value="Programming"]').isDisabled(), 'Name in another group cannot be selected');
            await form.locator('[data-category-group-filter]').fill('educat');
            check(await form.locator('[data-group-choice]:visible').count() === 3, 'Filter retains selected names and matching available names');
            await form.locator('[name="Members"][value="educatoin"]').check();
            await form.locator('[name="Members"][value="تعليم"]').uncheck();
            let submitted;
            await context.route('**/CategoryGroups/Save', async route => {
                submitted = new URLSearchParams(route.request().postData());
                await route.fulfill({status: 200, contentType: 'text/html', body: '<p>fixture only</p>'});
            });
            await form.locator('button[type="submit"]').click();
            await page.waitForURL('**/CategoryGroups/Save');
            check(submitted.getAll('Members').includes('educatoin') && !submitted.getAll('Members').includes('تعليم')
                && !submitted.getAll('Members').includes('Programming'), 'Approval submits exact added and removed names');
            check(submitted.get('Version') === 'AAAAAAAAAAA=', 'Concurrency version submitted');
            check(errors.length === 0, 'No group editor script errors');
            await context.close();

            const member = await browser.newContext({viewport: {width, height: 1100}, extraHTTPHeaders: {'X-Fixture-Role': 'Member'}});
            await member.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
            const preferences = await member.newPage();
            check((await preferences.goto(new URL(`/${language}/Member/MemberShellFixture/Preferences?categoryLimit=3&grouped=true`, base).href)).status() === 200, 'Grouped preferences render');
            check(await preferences.locator('[data-preference-quota]').getAttribute('data-selected') === '3', 'Aliases count as one group plus two fields');
            check(await preferences.locator('[name="Categories"]').count() === 3, 'Duplicate aliases collapse to one selected representative');
            check((await preferences.locator('[data-preference-selected="Categories"]').innerText()).includes('التعليم'), 'Group name explains selected representative');
            check(await preferences.locator('[name="addCategory"][value="تعليم"]').count() === 0, 'Same approved group is not offered as extra slot');
            check(await preferences.locator('[name="addCategory"][value="New field"]').isDisabled(), 'Fourth independent category still blocked');
            check(await preferences.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Grouped preferences fit viewport');
            const denied = await member.request.get(new URL(`/${language}/Admin/CategoryGroups`, base).href, {maxRedirects: 0});
            check([302, 401, 403].includes(denied.status()), 'Member cannot access group administration');
            await member.close();
        }
        console.log(`Category groups UI: ${passed} checks passed; no real approvals or alerts sent.`);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
