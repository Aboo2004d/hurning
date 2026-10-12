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
