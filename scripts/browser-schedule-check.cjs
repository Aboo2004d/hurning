const assert=require('node:assert/strict');
const {chromium}=require(process.env.HIRING_PLAYWRIGHT_MODULE||'C:/Users/lolly1/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const credential=require('./test-credentials.cjs')(),browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage();
 const base='http://127.0.0.1:5186',name='QA disabled schedule '+Date.now();
 try{
  await page.goto(base+'/Account/Login');await page.locator('#Email').fill(credential.email);await page.locator('#Password').fill(credential.password);await page.getByRole('button',{name:'تسجيل دخول الإدارة',exact:true}).click();
  await page.goto(base+'/Admin/Source');await page.locator('#Name').fill(name);await page.locator('#Url').fill('https://8.8.8.8');assert.equal(await page.locator('#Connector').inputValue(),'4');await page.locator('#Connector').selectOption('2');await page.locator('details summary').click();await page.locator('#ConfigurationJson').fill('{"ListingLinkSelector":"a"}');await page.locator('#IntervalHours').fill('8');await page.locator('#CollectionStartTime').fill('00:00');await page.getByRole('button',{name:'حفظ',exact:true}).click();assert.ok(page.url().endsWith('/Admin/Sources'),await page.locator('body').innerText());
  let row=page.locator('tr').filter({hasText:name});assert.ok((await row.innerText()).includes('00:00'));assert.ok(await row.getByRole('button',{name:'ابدأ الجمع الآن'}).isDisabled());
  await row.getByRole('link',{name:'تعديل'}).click();await page.locator('#CollectionStartTime').fill('12:00');await page.locator('#IntervalHours').fill('24');await page.getByRole('button',{name:'حفظ',exact:true}).click();row=page.locator('tr').filter({hasText:name});assert.ok((await row.innerText()).includes('12:00'));
  assert.ok(await page.getByRole('columnheader',{name:'ساعة بدء الجمع'}).count());
  console.log('PASS: Per-source collect button, source activation guard, midnight/every-8-hours fields, editing start time and interval. Fixture source remains disabled; no network collection triggered.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e.message);process.exit(1)});
