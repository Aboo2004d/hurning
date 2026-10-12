const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.HIRING_PLAYWRIGHT_MODULE||'C:/Users/lolly1/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const credential=require('./test-credentials.cjs')();const secrets={'Bootstrap:AdminEmail':credential.email,'Bootstrap:AdminPassword':credential.password};
 const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:1280,height:900}});page.setDefaultTimeout(15000);
 const base='http://127.0.0.1:5186',stamp=Date.now(),records=[];
 try{
  await page.goto(base+'/Account/Login');await page.locator('#Email').fill(secrets['Bootstrap:AdminEmail']);await page.locator('#Password').fill(secrets['Bootstrap:AdminPassword']);await page.getByRole('button',{name:'تسجيل دخول الإدارة',exact:true}).click();assert.ok(page.url().includes('/Admin'));
  for(const kind of ['Job','Training','Tender','ProcurementNotice']){
   const title=`QA ${kind} ${stamp}`;await page.goto(base+'/Admin/Edit');await page.locator('#edit-kind').selectOption(kind);await page.locator('#OriginalLanguage').selectOption('en');await page.locator('#Title').fill(title);await page.locator('#Organization').fill('QA type verification');await page.locator('#Locations').fill('Gaza');await page.locator('#summary-input').fill('Sample listing for local verification only.');await page.locator('[name=sourceUrl]').fill('https://example.com/'+kind.toLowerCase());await page.locator('#Status').selectOption('Published');
   assert.equal(await page.locator('[data-job-field]').first().isVisible(),kind==='Job');assert.equal(await page.locator('[data-training-field]').first().isVisible(),kind==='Training');assert.equal(await page.locator('[data-tender-field]').first().isVisible(),['Tender','ProcurementNotice'].includes(kind));
   if(kind==='Training'){await page.locator('#Duration').fill('Two weeks');await page.locator('#Cost').fill('Free');}if(['Tender','ProcurementNotice'].includes(kind))await page.locator('#ReferenceNumber').fill('QA-'+kind+'-'+stamp);
   await page.getByRole('button',{name:'حفظ',exact:true}).click();assert.ok(page.url().endsWith('/Admin/Opportunities'),'Save failed for '+kind);const href=await page.locator('tr').filter({hasText:title}).getByRole('link',{name:'تعديل'}).getAttribute('href');const id=href.split('/').pop();records.push({id,kind,title});
   await page.goto(base+'/?kind='+kind+'&q='+encodeURIComponent(title));assert.equal(await page.locator('.opportunity-card').count(),1);assert.equal(await page.locator('#employment-filters').isVisible(),kind==='Job');await page.locator('.opportunity-card').click();assert.equal(await page.locator('h1').textContent(),title);assert.equal(await page.locator('h1').getAttribute('lang'),'en');assert.equal(await page.locator('html').getAttribute('lang'),'ar');
  }
  const sitemap=await page.request.get(base+'/sitemap.xml');assert.equal(sitemap.status(),200);const xml=await sitemap.text();for(const record of records)assert.ok(xml.includes('/Home/Details/'+record.id));
  const robots=await page.request.get(base+'/robots.txt');assert.ok((await robots.text()).includes('Disallow: /Admin'));
  await page.goto(base+'/Admin/Source');await page.locator('#Name').fill('Invalid private source');await page.locator('#Url').fill('https://127.0.0.1');await page.locator('#Connector').selectOption('2');await page.locator('#ConfigurationJson').fill('{}');await page.getByRole('button',{name:'حفظ',exact:true}).click();assert.ok(await page.locator('.validation').innerText());assert.ok(!page.url().endsWith('/Admin/Sources'));
  await page.goto(base+'/Admin/Source');await page.locator('#Name').fill('Missing permission source');await page.locator('#Url').fill('https://example.com');await page.locator('#Connector').selectOption('2');await page.locator('#ConfigurationJson').fill('{"ListingLinkSelector":"a"}');await page.locator('#Enabled').check();await page.getByRole('button',{name:'حفظ',exact:true}).click();const permissionError=await page.locator('.validation').innerText();assert.ok(permissionError.includes('approval'),'Permission validation result: '+permissionError);
  const withoutCsrf=await page.request.post(base+'/Admin/Resolve',{form:{id:'1'}});assert.equal(withoutCsrf.status(),400);
  console.log('PASS: Four types, type-specific filters/fields, original English content in Arabic UI, sitemap, robots, private-source blocking, usage approval and CSRF.');
 }finally{
  for(const record of records){await page.goto(base+'/Admin/Edit/'+record.id);await page.locator('#Status').selectOption('Hidden');await page.getByRole('button',{name:'حفظ',exact:true}).click();}
  await browser.close();
 }
})().catch(e=>{console.error(e.message);process.exit(1)});
