# سجل تنفيذ المرحلة الأولى

المسار المعتمد: `F:\Uni\Files\Works\HiringPs`.
الخطة المرجعية: `Docs/PROJECT-PLAN.md`. النسخة خارج المشروع مرجع تاريخي فقط، والتحديثات الجديدة داخل هذا المسار.

## 11 أكتوبر 2026 — بدء التنفيذ والاستئناف
- إنشاء حل .NET 10 بمشاريع Core وWeb MVC وWorker مستقل.
- إضافة نموذج الفرص الأربع، المصادر، طابور المعالجة، مراجعات المحتوى، البلاغات وسجل التدقيق.
- إضافة جمع Playwright وفحص عناوين الشبكة، ومعالجة AI تحفظ العناصر عند غياب المفتاح.
- إضافة أساس الحساب الإداري والبحث والأرشيف واللغة؛ واجهة الإدارة والعرض لا تزال قيد الاستكمال.
- رصد SQL Server Express محلي يعمل. لم تُنشأ قاعدة المشروع بعد ولم ينجح فحص build حتى هذه النقطة.
- المصادر مرشحة وغير مفعلة افتراضيًا؛ لا مفاتيح AI أو Firecrawl ولا وصول إلى VPS، ولم يحدث نشر أو اختبار إنتاجي.
- استئناف العمل بعد توقف مراجعة الأدوات، ووضع الخطة والتوثيق داخل المشروع.

لا تُعتبر المرحلة مكتملة حتى اجتياز معايير الخطة؛ كل نتائج build وSQL والمتصفح والجمع ستسجل منفصلة.

## نتائج التنفيذ والتحقق المحلي
- بناء .NET 10 نجح دون أخطاء أو تحذيرات، مع حزم SQL/Identity/Playwright وإصدار AngleSharp محدث لمعالجة تحذير أمني في الإصدار الأول.
- توليد migration InitialSchema وDocs/SCHEMA.sql، وتطبيقه على HiringPs_Development. خمس مصادر مرشحة غير مفعلة افتراضيًا؛ لا إعلانات حقيقية نشرت.
- حساب إدارة تطوير واحد بكلمة مرور عشوائية محفوظة خارج المشروع في User Secrets؛ لا طباعة أو تضمين كلمات مرور.
- واجهة عربية/إنجليزية بخط Cairo: بحث وأنواع فرص وفلاتر وبطاقات وتفاصيل وأرشيف وبلاغات. محتوى الفرصة بلغته الأصلية.
- لوحة مصادر وإضافة وتعديل يدوي ومراجعة وطابور وأخطاء وبلاغات؛ دمج يدوي لمكرر يربط المصادر ويخفي النسخة المكررة.
- 23 فحصًا نجحت، بينها SQL حقيقي: idempotency والتحديث والمرجع المؤكد والحفاظ على إخفاء الأدمن والمواعيد المجهولة والأرشفة وقفل متزامن وانتظار AI وHeartbeat.
- اختبار متصفح محلي نجح: اللغة/RTL وLTR، حماية الإدارة، نشر يدوي ثم بحث وتفاصيل وبلاغ وحله، عرض الهاتف، إخفاء السجل وحجب تفاصيله عن الزوار. السجلات توضيحية وتُركت مخفية؛ لا وظائف سوق حقيقية.
- فحص أول بـPlaywright .NET/Edge: الديوان 70 رابطًا وعينة تفاصيل 2036 حرفًا، قبل إضافة فلترة مواعيد المنتهي؛ PalCareer تعثر في مهلة التنقل. النتيجة لا تثبت قبول المصادر للإنتاج.
- محاولة انتظار مبكر عند Commit تعثرت بمهلة 25 ثانية في المصدرين؛ أعيد انتظار DOMContentLoaded للديوان بناءً على نجاح مساره الأول. هذا اختلاف انتظار يحتاج تحققًا إضافيًا وليس نجاحًا مثبتًا للتعديل.
- تنزيل Chromium المحلي بطيء وأوقف بعد وصوله إلى 10% لتجنب قفل ملفات البناء أثناء الاختبار؛ فحص Edge لا يعوض اختبار إصدار Chromium المقترح على VPS.
- لم تنفذ طلبات Gemini/Firecrawl فعلية ولا نشر VPS، ولم تُستكمل مصادر الأنواع الأربعة أو parsing المرفقات.

سجل النتائج يستكمل بعد كل اختبار؛ لا تُحوّل المعايير غير المجتازة إلى حالة مكتملة.

## 11–12 أكتوبر 2026 — تحديث الأدلة السابقة
- اكتمل تنزيل Chromium ومتطلباته داخل المشروع، ونجح فحص محرك Playwright المخصص: الديوان 8 روابط غير منتهية، PalCareer 50 رابطًا، PRCS 8 عطاءات PDF وعينة نصية. عينات تشخيصية، وليست موافقات تفعيل أو نشر.
- أضيف PdfPig مع حدود 8MB و40 صفحة ورفض PDF غير النصي للمراجعة؛ نتائج قارئ PDF وقواعد التواريخ والتكرار صارت 34 فحصًا ناجحًا.
- أضيفت sitemap/robots وتغيير كلمة مرور الأدمن وملفات نشر محلية؛ لا AI بمفتاح حقيقي ولا نشر VPS.

## 12 أكتوبر 2026 — طلبات المستخدم التسعة
- قرأنا تعديلات المستخدم: اتصال قاعدة البيانات نُقل، Bootstrap استُبدل بـSeedData، والموقع يطبق المهاجرات تلقائيًا عند التشغيل.
- حفظنا بصمات ملفات المستخدم قبل العمل، وتأكدنا بعده أن Program.cs للويب وSeedData.cs وDatabase.cs وDesignTimeFactory.cs وappsettings ومهاجراته السابقة لم تتغير. تحديث snapshot مولّد وإضافة مهاجرات جديدة لحقول الساعة والطلبات فقط.
- أضيف CollectionStartTime ودورية ثابتة من مرساة محلية بتوقيت فلسطين، مع Retry منفصل يمنع تغيير الإيقاع عند الخطأ.
- أضيف زر جمع لكل مصدر؛ الطلب يُحفظ في SQL ويعالج المصدر المحدد فقط، مع منع الطلب المتكرر والاستئناف وتحديث حالة الصفحة تلقائيًا.
- 43 فحصًا ناجحًا تشمل عزل المصدر اليدوي ولو أن مصدرًا آخر مستحق، وبقاء الموعد الدوري، واستئناف دفعة دون إعادة اكتشاف أو جمع مصدر آخر.
- اختبارات الواجهة الأساسية والأنواع الأربعة والحماية والساعة/الدورية وزر الجمع نجحت. البيانات الاختبارية مخفية أو مصادر غير مفعلة، ولا محاولات جمع من عنوان IP التوضيحي.
- اختبرنا start-local وstop-local: يبدأ الويب بمهاجرات المستخدم ثم العامل، ويوقف عملياته الخاصة دون لمس عمليات المستخدم الأخرى؛ العامل يعمل محليًا دون أخطاء حالية بعد بناء النسخة المتوافقة.
- حذفنا Class1 وقالب CSS القديم وpartial تحقق غير مستعمل وجميع ملفات Bootstrap/jQuery غير المستخدمة. حذف المجلدات الفارغة نفسها حُجب بسياسة التنفيذ؛ لم نتجاوز الحجب. احتُفظ بملفات Visual Studio التابعة للمستخدم والمتصفح والمهاجرات والاختبارات.
- أضيف LOCAL-RUN وMANUAL-DEPLOYMENT مع ما سيُثبّت على Windows VPS وخطوات النشر اليدوي والبدء بعد إعادة التشغيل. لم تُثبت IIS أو خدمة Windows محليًا، ولم يُنشر إلى VPS.
- مفتاح Gemini ما زال غير منشأ؛ لا اعتماد نهائي للتصنيف أو وعد نشر آلي كامل عند غياب المفتاح.
- نجح publish اليدوي لكلا المشروعين: web.config للموقع وHiringPs.Worker.exe win-x64 للعامل، داخل artifacts/manual-publish. لم يُنشر إلى سيرفر فعلي.

## 2026-10-12 — Automatic source discovery
Added Automatic connector as the default only for newly added sources; existing connectors and database/seed configuration preserved. Optional JSON moved under advanced settings. Rendered DOM discovery, same-host detail candidates, URL pagination, semantic content extraction, bounded settling, and conservative rejection of unknown layouts. No schema migration required (existing integer connector column). 47 local checks passed, including temporary SQL integration; no claim of universal source coverage or live AI validation.


## 2026-10-12 — Admin mobile layout
Admin tables previously compressed all columns into phone width. Added labeled stacked records below 640px and wrapped admin navigation/actions, preserving desktop tables and public cards. Collection discovery and raw content fetch run without AI credentials; structured extraction/summary/validation/publication wait as AwaitingAi (six-hour retry) when key is missing. Database/seed files unchanged.

Validation: read-only browser checks passed across five admin pages at 360, 390, 640 and 1280px in Arabic and English, including long diagnostic text. Static CSS/JS updates require page refresh; no database migration or process restart needed.

## 2026-10-12 — Admin action buttons
Source Edit and Collect Now actions now share one flex row and matching green outline buttons with hover/focus and disabled states. Applied the outline style to admin Add, Save, Retry, Resolve and Merge controls. Desktop tables and phone records preserved; no database or collector behavior changes. Release build passed; five admin pages checked in Arabic/English at phone and desktop widths.

## 2026-10-13 — UNRWA HTTP 403 diagnosis
Read-only local Edge/Playwright probes confirmed www.unrwa.org/careers/vacancies returns HTTP 403 with Cloudflare Just a moment challenge. careers.un.org returned CloudFront Request blocked HTTP 403. inspira.un.org root returned HTTP 200 then redirected to a login page, so that status does not establish a public vacancy-list collector. Official UNRWA recruitment guidance identifies Inspira as its advertising platform. No source settings or database records changed; no anti-bot bypass, proxy rotation or simulated approval added. UNRWA collection remains unresolved pending an accessible public official listing/feed or approved access; existing working sources unaffected.

Additional result: a published public Inspira detail URL (JobOpeningId=278490) returned HTTP 200 and actual UNRWA vacancy text locally, without login. This was an expired Amman listing used only as a read-only connectivity fixture, not a Palestinian job to publish. Public detail access is possible; complete discovery/pagination for Palestinian vacancies is still unverified. No replacement connector is marked ready.

## 2026-10-13 — Inspira discovery continuation
Read-only follow-up: public /psc/UNCAREERS/EMPLOYEE/HRMS/ returned HTTP 400 (no content type). /psp/UNCAREERS/?cmd=login&languageCd=ENG returned HTTP 200 but redirected to PUNA1J applicant login; visible links were registration/password reset/contact, not public vacancy discovery. No authentication attempted and no identifier enumeration. Public details remain reachable, but a complete listing route is not verified. No connector enum, SeedData, migration or source records changed. Asked whether official careers.un.org listing works in the user ordinary browser to distinguish environment-specific access from a universally available route.

## 2026-10-13 — Bounded HTTP 403 wait
Added BrowserNavigation shared by automatic discovery, configured selectors and detail retrieval. Initial 403 waits up to 20 seconds (ForbiddenWaitSeconds, clamped 0–30) for automatic main-document navigation to HTTP 2xx with ready visible content; blocked/challenge content remains rejected. No refresh loop, security bypass or source/database/SeedData changes. Four browser navigation checks cover delayed recovery, persistent 403, immediate 404 and cancellation.

Validation: Release build passed, 47 existing checks passed and 4 browser navigation checks passed. Live Chromium probes: UNRWA vacancies and careers.un.org/jobopening both remained HTTP 403 after the 20-second wait. Waiting does not resolve these sources in the current network. Publish packages refreshed; local web/worker restarted.

## 2026-10-13 — Official UNRWA RSS + Inspira connector
User-supplied saved HTML exposed official careers.un.org/jobfeed. Direct safe .NET RSS access succeeded; 428 total entries and 7 UNRWA Palestine matches. Two live details (285939, 285511) read by the actual BrowserCollector with 14,924 and 17,091 characters. No probe records saved/published. Added UnrwaInspira enum and guarded official feed URL, bounded/pinned public HTTP fetch, safe XML parsing, employer/duty-station filtering, deterministic Inspira ID mapping. SeedData source changes explicitly authorized: new UNRWA default and idempotent upgrade of old default URL only, retaining approval, Enabled, schedule and linked data. No schema migration or database recreation. Release build passed; 49 checks including SQL integration passed.

Live end-to-end verification: existing UNRWA row was upgraded successfully and per-source manual collection queued. Seven source items discovered; three initially stored as AwaitingAi, three had transient TimeoutException retries, and the last was still processing at inspection. A retry fixture (285542) subsequently fetched successfully in an isolated actual collector probe (16,675 chars). Partial network timeouts remain distinct from the resolved initial HTTP403 discovery route; do not claim every detail or AI publication passed.
Final manual-run snapshot: request completed; 7 items discovered, 3 AwaitingAi with saved text and 4 Retry due detail timeouts. Worker returned Idle. Normal retry scheduling remains enabled. This is successful source discovery with partial detail retrieval, not seven fully processed/published opportunities.

## 2026-10-13 — Jobs.ps saved latest page diagnosis
User supplied latest-jobs HTML: 20 visible records, detail /jobs/slug-id links, page= pagination and official /rss index. Live local HTTP latest URL returned 403; normal headless Edge confirmed Arabic Cloudflare security-check page. Web research retrieved the public RSS index and found /rss/jobs/gaza-jobs returns XML to the research fetch, but that does not establish local worker access. Source terms explicitly restrict reuse/publication; no source approval or records changed and no protection bypass introduced.


## 2026-10-13 — Jobs.ps multi-city RSS check
Tested 22 official in-scope location feed URLs with bounded parallelism (2). Gaza returned HTTP200, valid RSS, 15 unique items and 15 descriptions >=80 chars (shortest222); other21 returned403. No overall feed reliability or complete-ad guarantee. Read-only source checks; no SeedData/database/source-permission edits. Detailed evidence: Docs/JOBS-RSS-CHECK.md and ignored artifacts/jobs-rss-check.json. Repro script: scripts/jobs-rss-check.ps1.


## 2026-10-13 — ReliefWeb saved page recheck
Saved jobs HTML includes official /jobs/rss.xml. Fresh local HTTP probes returned jobs HTML200 with20 detail links and RSS200 with20 items. This is current diagnostic evidence, superseding earlier generic403 assumptions for these endpoints only. No source/SeedData/database edits; API adapter still pending approved appname and implementation. RSS is a separate website endpoint, not proof that the API accepts an unapproved identifier or that global20 items cover Palestine.

## 2026-10-13 — Sources table horizontal scrolling
Source table headers and cell text now remain on a single line with width determined by content and horizontal scrolling inside the existing wrapper. This table stays a table on phones instead of receiving stacked admin-record styling; other admin tables retain their existing behavior. Edit/Collect actions remain aligned. UI-only change; program not started and no database/seed changes.


## 2026-10-13 — Opportunities/review table layout
Applied existing table-nowrap styling to the opportunities table (including review listings): headers, values and Edit action remain on one line; table scrolls horizontally within its wrapper. Reuses the sources table behavior on phone/desktop. No program launch, AI logic or database changes.

