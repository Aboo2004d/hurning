# النشر كموقع واحد (بدون خدمة العامل)

منذ 25 أكتوبر 2026 يستطيع الموقع تشغيل حلقات الخلفية بنفسه: الجمع والمعالجة بالذكاء الاصطناعي، فحص الأدوات، تنبيهات تليجرام للأعضاء، النشر في القناة وIndexNow. يكفي `publish` واحد لمشروع `HiringPs.Web`.

## الإعداد

- `Collector:RunInWeb = true` في `src/HiringPs.Web/appsettings.json` (الافتراضي في الكود false).
- `Ai:ApiKey` يجب أن يكون مفتاح Gemini حقيقيًا في إعدادات الموقع، وإلا تبقى العناصر «بانتظار الذكاء الاصطناعي» ولا تُنشر.
- لا تشغّل خدمة `HiringPsCollector` المستقلة بالتوازي مع هذا الوضع. إن كانت مثبتة: أوقفها وعطّلها، أو اجعل `Collector:RunInWeb = false`.

## حجم النشر: المتصفح والأدوات خارج النشر

افتراضيًا لا يحمل النشر المتصفح ولا أدوات PDF (نحو 150 MB فقط، معظمها مشغّل Playwright الضروري):

- **المتصفح:** يستخدم الإنتاج Microsoft Edge المثبت على السيرفر عبر `Browser:Channel = msedge` في `appsettings.Production.json`.
- **أدوات PDF:** تُرفع مرة واحدة إلى مجلد ثابت خارج مجلد الموقع، يحدده `Pdf:ToolsRoot` (الافتراضي `C:\HiringRuntime`). مجلد خارجي لأن ملف النشر يحذف محتوى مجلد الوجهة (`DeleteExistingFiles`).
  1. شغّل محليًا: `scripts/prepare-runtime.ps1` ← ينتج `artifacts/runtime` (نحو 240 MB).
  2. انسخ محتواه إلى `C:\HiringRuntime` على السيرفر (يحوي `Tools` و`tessdata`).
  3. أعد النسخ فقط عند تحديث Poppler أو Tesseract.
- ملف النشر مضبوط على `win-x64` فقط لحذف ملفات Linux وmacOS.

بديل بلا Edge: انشر بـ `-p:BundleRuntime=true` ليحمل النشر Chromium الخفيف والأدوات داخل مجلد الموقع (نحو 630 MB)، واحذف `Browser:Channel` و`Pdf:ToolsRoot` من `appsettings.Production.json`. أو استعمل `prepare-runtime.ps1 -IncludeBrowser` واضبط `Browser:BrowsersPath` إلى `C:\HiringRuntime\.playwright-browsers`.

Chromium الكامل (433 MB) لا يُنشر أبدًا: الكود يعمل بوضع headless الذي يستخدم النسخة الخفيفة فقط.

## إعدادات IIS المطلوبة (مرة واحدة)

كلها تُطبّق تلقائيًا بالسكربت (PowerShell كمسؤول على السيرفر):
`scripts\configure-iis-runtime.ps1 -SiteName "<اسم الموقع في IIS>" -Recycle`

- Application Pool: `Start Mode = AlwaysRunning`، `Idle Time-out = 0`، وتعطيل إعادة التشغيل الدورية أو جعلها في وقت هادئ.
- الموقع: `Preload Enabled = True`.
- Application Pool: `Load User Profile = True` (مطلوب لتشغيل المتصفح).
- هوية الـ Application Pool تحتاج قراءة وتنفيذ على مجلد الموقع و`C:\HiringRuntime`، وكتابة على `data/` و`logs/` داخل مجلد الموقع.

## التحقق بعد النشر

- لوحة الإدارة ← الأدوات: يظهر نبض العامل باسم السيرفر، وحالة المتصفح وPDF والذكاء الاصطناعي.
- لوحة الإدارة ← المصادر: آخر تشغيل لكل مصدر وأي تحذير صحة.

## العودة إلى خدمة منفصلة

اجعل `Collector:RunInWeb = false` في الموقع، وانشر `HiringPs.Worker` وثبّته بـ `scripts/install-worker.ps1`. الكود نفسه مشترك في `HiringPs.Core/Features/Background`.

محليًا، `scripts/start-local.ps1` يشغّل العامل منفصلًا ويطفئ الحلقات داخل الموقع تلقائيًا.
