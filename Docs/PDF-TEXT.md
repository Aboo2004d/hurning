# PDF وOCR — التشغيل دون الاحتفاظ بالملفات

الحالة الحالية: يُنزّل PDF ويُعالج داخل مجلد مؤقت خاص، ثم تُحذف نسخة الملف وصور الصفحات ومخرجات النص وTSV بعد النجاح أو الفشل أو الإلغاء. **لا يُنشأ أرشيف PDF أو ملفات نصوص وتقارير دائمة.** النص المستخرج وأدلة التواريخ وأرقام الصفحات تبقى ضمن سجلات قاعدة البيانات الحالية للتحقق والمراجعة، وروابط المصادر تبقى روابط خارجية.

لا يلزم تغيير قاعدة البيانات لهذه السياسة. صفحة «الأدوات» التي أضيفت في الدفعة نفسها ما زالت تحتاج مهاجرة ToolMonitoring؛ شغّل الموقع أولًا لتطبيقها ثم أعد تشغيل العامل.

## الفرق بين ملفات الإعلانات وبيانات اللغات

`ara.traineddata` و`eng.traineddata` هما نموذجا التعرف على العربية والإنجليزية في Tesseract. ينزّلان مرة واحدة ويُستخدمان في كل عملية OCR. ليسا إعلانات ولا ملفات PDF نزلها المجمّع، ولا يُحذفان بعد المعالجة.

```powershell
$ocrData = Join-Path (Get-Location) 'src\HiringPs.Worker\tessdata'
New-Item -ItemType Directory -Path $ocrData -Force | Out-Null

Invoke-WebRequest `
    'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/ara.traineddata' `
    -OutFile "$ocrData\ara.traineddata"

Invoke-WebRequest `
    'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/eng.traineddata' `
    -OutFile "$ocrData\eng.traineddata"
```

الأمر الأول ينشئ مجلد اللغات إن لم يكن موجودًا. كل Invoke-WebRequest ينزّل بيانات لغة إلى المجلد. لا تشغّل التنزيل مع كل إعلان؛ أبق الملفين مثبتين. لا يستخدم النظام خدمة OCR مدفوعة أو Python أو Node منفصلًا لهذه القراءة.

## إعداد Windows المحلي

1. ثبّت [توزيعة Poppler لـWindows](https://github.com/oschwartz10612/poppler-windows/releases) مع `pdftotext.exe` و`pdftoppm.exe` وكل DLLs التابعة لها. هذا توزيع Windows مجتمعي؛ [مشروع Poppler](https://poppler.freedesktop.org/) هو المرجع الأصلي.
2. ثبّت Tesseract 5.x x64 من مصدر موثوق مثل روابط Windows في [دليل التثبيت](https://tesseract-ocr.github.io/tessdoc/Installation.html).
3. نزّل بيانات اللغتين بالأوامر أعلاه. SHA256 لملف العربية 4.1.0 المختبر هو `e3206d3dc87fd50c24a0fb9f01838615911d25168f4e64415244b67d2bb3e729`.
4. ضع توزيعة Poppler كاملة في `src/HiringPs.Worker/Tools/poppler-26.09.0` وتوزيعة Tesseract كاملة في `src/HiringPs.Worker/Tools/Tesseract-OCR`، وملفي اللغات في `src/HiringPs.Worker/tessdata`. احتفظ بملفات DLL والتراخيص. قسم `Pdf` التالي يستخدم نفس ترتيب مجلداتك؛ غيّر اسم مجلد الإصدار فقط إن اختلف.

```json
"Pdf": {
    "TextExecutable": "Tools/poppler-26.09.0/Library/bin/pdftotext.exe",
    "RenderExecutable": "Tools/poppler-26.09.0/Library/bin/pdftoppm.exe",
    "OcrExecutable": "Tools/Tesseract-OCR/tesseract.exe",
    "OcrDataPath": "tessdata",
    "WorkDirectory": "data/pdf-work",
    "EnableOcr": true,
    "RequireOcrReview": true,
    "ProcessTimeoutSeconds": 45
}
```

هذا قسم يضاف داخل كائن الإعدادات الموجود، وليس بديلًا عن الملف كاملًا؛ حافظ على أقسام SQL وAI الموجودة. نفس ملف `src/HiringPs.Worker/appsettings.json` يُستخدم أيضًا أثناء التطوير المحلي، بما فيه التشغيل من Visual Studio أو `dotnet run` في بيئة Development؛ لا تحتاج إلى إنشاء ملف تطوير منفصل لإعدادات PDF. ملفات `appsettings.{Environment}.json` المعتادة يمكنها تخصيص القيم للبيئة عند الحاجة. إعدادات PDF لا تحتاج User Secrets أو جلسة PowerShell مخصصة.

من داخل المشروع شغّل:

```powershell
.\scripts\configure-pdf.ps1
```

السكربت الآن **فحص فقط**: لا يطلب مسارات، ولا يكتب متغيرات بيئة أو Registry، ولا ينزّل أو يثبت أدوات. يشغّل فحص العامل بالإعدادات المقروءة من JSON ويتحقق من الأدوات واللغتين وإنشاء الملفات المؤقتة وحذفها. لا يشترط تشغيل التطبيق من الجلسة نفسها. الملف ينسخ إلى مخرجات build/publish بالطريقة المعتادة؛ عدّل ملف المصدر ثم أعد البناء، وأعد تشغيل العامل بعد تعديل الإعدادات.

```powershell
dotnet run --project src/HiringPs.Worker -c Release -- --pdf-check
dotnet run --project src/HiringPs.Worker -c Release -- --pdf-read 'C:\Samples\tender.pdf'
```

أمر القراءة يطبع طريقة كل صفحة والثقة، ويحذف **نسخة العمل** التي أنشأها. لا يحذف الملف الذي أعطيته في `C:\Samples` لأنه ملفك الأصلي، وليس نسخة تنزيل/معالجة أنشأها النظام. الفحص لا يجمع ولا ينشر ولا يحتاج SQL أو طلب AI.

## الاستضافة على Windows VPS

عند وضع الأدوات واللغات بالمجلدات السابقة، ينسخها build وpublish تلقائيًا إلى مجلد العامل مع الحفاظ على ترتيبها. ارفع مخرجات Worker كاملة إلى Windows VPS بنفس المعمارية المناسبة للأدوات؛ لا يلزم تثبيت منفصل إذا كانت التوزيعات مكتملة واعتمادياتها متاحة. لا يقوم publish بتنزيل الأدوات أو نماذج اللغات نيابةً عنك. يحتاج حساب خدمة العامل قراءة/تنفيذ الأدوات وDLLs وقراءة Tessdata، وصلاحية إنشاء **وحذف** الملفات في مجلد العمل. لا يلزم مجلد PDF مشترك مع IIS ولا صلاحيات قراءة أرشيف لحساب الموقع.

بعد تحديث Web وWorker معًا وتطبيق المهاجرة من الموقع، افتح `appsettings.json` الموجود **داخل مجلد Worker المنشور**، واحتفظ بقسم `Pdf` ذي المسارات النسبية؛ لا يلزم تغيير حرف القرص أو مسار النشر. تأكد أن حساب الخدمة يستطيع إنشاء الملفات وحذفها في مجلد العمل. إعدادات PDF تخص العامل؛ لا يلزم تكرارها في ملف Web. يمكن استخدام `appsettings.Production.json` للتخصيص المعتاد، مع الحفاظ على الإعدادات عند نشر تحديث جديد.

```powershell
.\configure-pdf.ps1 -WorkerDirectory 'C:\Apps\HiringPs\Worker'
Restart-Service HiringPsCollector
```

السكربت يفحص JSON المنشور ولا يغير إعدادات الخدمة أو حسابها. الفحص يجري بحساب PowerShell؛ تحقق أيضًا من صلاحيات حساب الخدمة وافتح صفحة «الأدوات» لرؤية الفحص من ذلك الحساب. لا تحتاج صلاحية مسؤول للفحص نفسه؛ إعادة تشغيل الخدمة تتبع صلاحيات إدارة Windows المعتادة. الاستضافة المشتركة التي تمنع تشغيل العمليات الخارجية تحتاج عاملًا على جهاز يسمح بتشغيلها.

من Worker المنشور يمكن تشغيل `HiringPs.Worker.exe --pdf-check` أو `--pdf-read` دون SDK. تعليمات IIS والخدمة العامة في [النشر اليدوي](MANUAL-DEPLOYMENT.md).

## الإعدادات ومسار المعالجة

| المفتاح داخل قسم Pdf | الوظيفة |
| --- | --- |
| `Pdf:TextExecutable` | مسار pdftotext، والافتراضي اسمه من PATH |
| `Pdf:RenderExecutable` | مسار pdftoppm |
| `Pdf:OcrExecutable` | مسار tesseract |
| `Pdf:OcrDataPath` | مجلد نماذج اللغات، دائم ولا يُنظَّف |
| `Pdf:WorkDirectory` | مجلد مؤقت خاص؛ الافتراضي hiring-pdf-work داخل temp الخاص بحساب العامل |
| `Pdf:EnableOcr` | true افتراضيًا؛ تعطيله يحيل الصفحات المحتاجة إليه للمراجعة |
| `Pdf:RequireOcrReview` | true افتراضيًا؛ اشتراط مقارنة بصرية مع الأصل لدى المصدر |
| `Pdf:ProcessTimeoutSeconds` | 45 افتراضيًا، ويمكن تقليله من 1 إلى 45 |

Poppler يقرأ طبقة النص مع حدود الصفحات. العربية والصور وصفحات النص غير المقروء تُقرأ بصريًا باستخدام ara+eng، دون عكس الأرقام أو تخمين الكلمات. تُعالج كل صفحة؛ لا يُتجاهل محتوى صورة لأن صفحة أخرى لها نص. غياب الأدوات أو اللغة، التالف والمشفر، انخفاض الثقة أو تجاوز الحدود يولّد مراجعة واضحة، ولا يُرسل نص جزئي إلى AI على أنه مستند مكتمل.

الحدود: 8 MB، و40 صفحة، وحتى ثلاثة مرفقات PDF للإعلان، و40 ثانية للتنزيل، و3 دقائق لمعالجة المستند، وأقصى بُعد للصورة 2500 بكسل، وحد المخرج النصي 2 MB. العمليات الخارجية تنتهي مع الإلغاء/المهلة ثم تُحذف ملفات العمل. إذا منع النظام الحذف يعاد ثلاث مرات، ثم يُعلن فشل التنظيف بدل اعتباره نجاحًا صامتًا. لا يمكن ضمان حذف فوري عند قتل العملية أو انقطاع الكهرباء؛ في تشغيل لاحق تُنظّف مجلدات العمل التابعة للتطبيق والأقدم من ساعة، مع حماية المحاولات النشطة بقفل ملف. لا تُحذف مجلدات أو ملفات عشوائية.

OCR قد يخطئ حتى بثقة عالية. لذلك يبقى النص الذي استخدم OCR للمراجعة البصرية افتراضيًا. المقارنة من الإعلان الأصلي ورقم الصفحة الظاهر؛ لا يوجد رابط تنزيل نسخة محلية محفوظة. تعطل رابط المصدر لاحقًا يعني أن النسخة الثنائية الأصلية غير متاحة لدينا. يمكن تغيير شرط المراجعة بـRequireOcrReview=false بعد قبول دقة عيناتك، ثم إعادة المحاولة لتطبيق السياسة الجديدة.

«إعادة المحاولة» لملف PDF أو إعلان ذي مرفقات تعيد الجلب والقراءة بدل النص القديم؛ تبقى حماية المنشور والمؤرشف وقرارات الأدمن. ارتفع إصدار قراءة PDF إلى 2 حتى لا يعاد استخدام استخراج سابق عند تغير سياسة القراءة.

## التخلص من الأرشيف السابق

إذا استُخدم الإصدار الذي يحفظ الأرشيف، اترك قيمة `Pdf:TextArchivePath` القديمة مؤقتًا عند التحديث. تنظيف PDF يزيل فقط الملفات التي أنشأها ذلك المسار: أسماء SHA256 متبوعة بـ.pdf أو .original.txt أو .readable.txt أو .json وملفاتها المؤقتة المعروفة. الإعداد القديم يستخدم للتنظيف فقط، ولا تُكتب ملفات جديدة فيه. إذا لم تضبطه، يفحص الموقع الافتراضي السابق logs/pdf-text تحت مجلد العامل. أوقف النسخة القديمة قبل تشغيل الجديدة؛ وإذا نقلت مجلد النشر فحدد مسار الأرشيف السابق الكامل. لا تُحذف ملفات اللغات أو الملفات غير المطابقة. يلزم أن يملك حساب العامل صلاحية حذف هذه الملفات؛ يمكن لمسؤول الجهاز تنظيف البقية إذا منعته الصلاحيات.

لا يتضمن التنظيف ملف PDF تختبره يدويًا من جهازك، ولا عينات الاختبار الصناعية الموزعة مع التطبيق، ولا أدوات التنفيذ ونماذج اللغات. المطلوب حذفه هو ملفات الإعلانات التي أنشأتها المعالجة ونسخ الأرشيف القديمة التابعة لها.

## إزالة إعدادات PDF القديمة إن استُخدمت

إعدادات .NET تحتفظ بالأولوية المعتادة: متغيرات البيئة/وسائط التشغيل قد تتجاوز JSON. إذا سبق أن شغّلت السكربت القديم بنجاح، أزل متغيرات PDF القديمة فقط من الجلسة أو أغلق تلك الجلسة وافتح جلسة نظيفة:

```powershell
Get-ChildItem Env: |
    Where-Object { $_.Name -like 'Pdf__*' } |
    ForEach-Object { Remove-Item -LiteralPath ('Env:' + $_.Name) }
```

إذا استخدمت سابقًا خيار ServiceName في السكربت القديم، افتح PowerShell كمسؤول وأزل إدخالات PDF فقط من Environment الخاص بالخدمة، مع إبقاء SQL وAI والمتصفح:

```powershell
$serviceKey = 'HKLM:\SYSTEM\CurrentControlSet\Services\HiringPsCollector'
$settings = Get-ItemProperty -LiteralPath $serviceKey -ErrorAction Stop
$existing = @($settings.Environment)
$kept = @($existing | Where-Object { $_ -and (($_ -split '=', 2)[0] -notlike 'Pdf__*') })
if ($settings.PSObject.Properties.Name -contains 'Environment')
{
    Set-ItemProperty -LiteralPath $serviceKey -Name Environment -Value ([string[]]$kept)
}
```

بعدها أعد تشغيل الخدمة. احتفظ بمسار الأرشيف القديم عند الحاجة للتنظيف داخل `Pdf:TextArchivePath` في JSON. إذا لم تستخدم السكربت القديم بنجاح فلا يلزم تنفيذ تنظيف متغيرات الخدمة.

## Linux والفحوص

```bash
sudo apt-get update
sudo apt-get install poppler-utils tesseract-ocr tesseract-ocr-ara tesseract-ocr-eng
tesseract --list-langs
```

يجب ظهور ara وeng. عدّل Pdf في JSON إلى أسماء الأدوات المثبتة أو مسارات Linux الفعلية ومجلد العمل المناسب؛ مسارات Windows المرفقة لا تعمل على Linux. فحوص التنفيذ:

```powershell
dotnet build HiringPs.sln -c Release
dotnet run --project tests/HiringPs.Checks -c Release -- --pdf-check
dotnet run --project tests/HiringPs.Checks -c Release -- --tools-check
dotnet run --project tests/HiringPs.Checks -c Release -- --workflow-check
```

العينات [صناعية](../tests/HiringPs.Checks/Fixtures/Pdf/README.md)، والفحوص تشمل النصي والمصور والمختلط، والحذف بعد النجاح والفشل والإلغاء، وتنظيف الأرشيف السابق، وعدم حذف نماذج اللغات أو الملفات غير التابعة للتطبيق أو العمل النشط. صفحة [الأدوات](TOOLS.md) تفحص مجلد العمل المؤقت والحذف وقراءة PDF فعلية من العامل. لا يثبت نجاح Linux صحة صلاحيات Windows/IIS أو SQL؛ اختبارها يحتاج بيئتك. المصدر المرجعي للهلال الأحمر أعاد 403 في التجربة السابقة ولم يُدع اختباره.


نتيجة التحقق من سياسة الحذف: نجح بناء Release و43 فحص PDF فعليًا، إضافة إلى 29 فحص أدوات و39 فحص معالجة سابقًا (111 فحصًا إجمالًا). شملت الفحوص حذف ملفات النجاح والفشل والإلغاء، وحماية العمل النشط وبيانات اللغات والملفات غير التابعة للنظام. فُحصت صياغة سكربت PowerShell؛ تشغيل Windows وحساب الخدمة وواجهة SQL يحتاج بيئتك.

### أساس المسارات النسبية وحزمة النشر

جميع مسارات أدوات PDF التي تتضمن مجلدًا، ومسار `OcrDataPath`، ومسار العمل النسبي تُحسب من `AppContext.BaseDirectory`، أي مجلد ملفات تشغيل العامل. لا تعتمد على مجلد PowerShell أو مجلد Windows Service. الأسماء المفردة مثل `pdftotext` تبقى مدعومة للبحث في PATH، والمسارات الكاملة تبقى مدعومة. إعدادات Windows المرفقة لا تشغّل أدوات Windows على Linux.

في التطوير تُنسخ `Tools` و`tessdata` إلى `bin/Release/net10.0` أو مخرجات Debug. وعند `dotnet publish` تُنسخان إلى مجلد النشر. `data/pdf-work` مجلد عمل مؤقت ينشأ أثناء التشغيل، ولا يضاف إلى الحزمة أو المستودع. بعد نقل الحزمة نفّذ `--pdf-check` ثم فحص الأدوات من لوحة الإدارة بحساب العامل. تضمين Chromium مستقل عن هذه الإضافة.
