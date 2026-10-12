// Local UI verification only. Credentials are read from User Secrets, never logged.
const fs=require('node:fs');const path=require('node:path');const assert=require('node:assert/strict');
const {chromium}=require(process.env.HIRING_PLAYWRIGHT_MODULE||'C:/Users/lolly1/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const credential=require('./test-credentials.cjs')();const secret={'Bootstrap:AdminEmail':credential.email,'Bootstrap:AdminPassword':credential.password};
 const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:1280,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(15000);
 const base='http://127.0.0.1:5186';
 const testTitle='اختبار تطوير — محاسب '+Date.now(),reportText='بلاغ اختبار تطوير '+Date.now();
 await page.goto(base);assert.equal(await page.locator('html').getAttribute('lang'),'ar');assert.ok(await page.getByRole('heading',{name:'فرصتك القادمة، أقرب.'}).count());
 await page.getByRole('button',{name:'English',exact:true}).click();assert.equal(await page.locator('html').getAttribute('dir'),'ltr');assert.ok(await page.getByRole('heading',{name:'Your next opportunity, closer.'}).count());
 await page.getByRole('button',{name:'العربية',exact:true}).click();
 await page.goto(base+'/Admin');assert.ok(page.url().includes('/Account/Login'));
 await page.locator('#Email').fill(secret['Bootstrap:AdminEmail']);await page.locator('#Password').fill(secret['Bootstrap:AdminPassword']);await page.getByRole('button',{name:'تسجيل دخول الإدارة',exact:true}).click();assert.ok(page.url().includes('/Admin'));
 await page.goto(base+'/Admin/Opportunities');
 const oldTestLinks=await page.locator('tr').filter({hasText:'اختبار تطوير — محاسب'}).filter({hasText:'جهة اختبار محلية'}).getByRole('link',{name:'تعديل'}).evaluateAll(els=>els.map(el=>el.getAttribute('href')));
 for(const href of oldTestLinks){await page.goto(base+href);await page.locator('#Status').selectOption('Hidden');await page.getByRole('button',{name:'حفظ',exact:true}).click();}
 await page.goto(base+'/Admin/Edit');await page.locator('#Title').fill(testTitle);await page.locator('#Organization').fill('جهة اختبار محلية');await page.locator('#Locations').fill('غزة');await page.locator('#summary-input').fill('سجل اختبار للواجهة فقط؛ ليس إعلانًا حقيقيًا.');await page.locator('#Category').fill('المحاسبة');await page.locator('[name=sourceUrl]').fill('https://example.com/local-ui-check');await page.locator('#Status').selectOption('Published');await page.getByRole('button',{name:'حفظ',exact:true}).click();
 assert.ok(page.url().endsWith('/Admin/Opportunities'),'Manual save failed: '+await page.locator('body').innerText());
 const editHref=await page.locator('tr').filter({hasText:testTitle}).getByRole('link',{name:'تعديل'}).getAttribute('href');const id=editHref.split('/').pop();
 await page.goto(base+'/?q='+encodeURIComponent(testTitle));assert.equal(await page.locator('.opportunity-card').count(),1);
 await page.locator('.opportunity-card').click();assert.ok(await page.getByRole('heading',{name:testTitle}).count());
 await page.locator('.report-panel summary').click();await page.locator('[name=reason]').selectOption('incorrect');await page.locator('[name=details]').fill(reportText);await page.getByRole('button',{name:'إرسال',exact:true}).click();assert.ok(await page.locator('.notice').filter({hasText:'وصل بلاغك'}).count());
 await page.goto(base+'/Admin/Reports');const reportRow=page.locator('tr').filter({hasText:reportText});await reportRow.getByRole('button',{name:'إغلاق البلاغ'}).click();assert.ok(await page.locator('tr').filter({hasText:reportText}).getByText('تمت المراجعة').count());
 await page.goto(base);fs.mkdirSync(path.join(__dirname,'../artifacts'),{recursive:true});await page.screenshot({path:path.join(__dirname,'../artifacts/web-desktop.png'),fullPage:true});
 await page.setViewportSize({width:375,height:900});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await page.screenshot({path:path.join(__dirname,'../artifacts/web-mobile.png'),fullPage:true});
 await page.goto(base+'/Admin/Edit/'+id);await page.locator('#Status').selectOption('Hidden');await page.getByRole('button',{name:'حفظ',exact:true}).click();
 await page.goto(base+'/?q='+encodeURIComponent(testTitle));assert.equal(await page.locator('.opportunity-card').count(),0);const detailResponse=await page.request.get(base+'/Home/Details/'+id);assert.equal(detailResponse.status(),404);
 assert.deepEqual(errors,[]);console.log('PASS: Arabic/English, admin authentication, manual publish, search, details, report and resolve, mobile layout, hide and public access restrictions. Test record left hidden.');await browser.close();
})().catch(e=>{console.error(e.message);process.exit(1)});
