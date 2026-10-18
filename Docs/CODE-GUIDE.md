# دليل قراءة المشروع

ابدأ من صفحة تريد تعديلها: Controller يعالج الطلب، View تعرض النتيجة، وخدمات Core تنفذ قواعد الحفظ والتحقق. لا تضع قواعد منح الاشتراك في JavaScript أو داخل القالب.

## Web: الصفحات واللغة

- `Program.cs`: تسجيل الخدمات والحماية والاتصال بقاعدة البيانات وترتيب middleware.
- `Infrastructure/LanguageUrls.cs`: اختيار بادئة اللغة وتصحيحها وتبديلها مع حفظ بقية الرابط.
- `Infrastructure/SiteLocalization.cs`: اللغتان والتقويم الميلادي.
- `Infrastructure/SiteRoutes.cs` و`AdminActionRoutes.cs`: المسارات وروابط الإدارة السابقة.
- `Controllers/HomeController.cs`: قائمة الفرص والبحث وملفات محركات البحث.
- `Controllers/OpportunitiesController.cs`: التفاصيل والتحقق من حق عرضها والبلاغات.
- `AboutController` و`ContactController` و`PrivacyController`: صفحة لكل مسؤولية.
- `AccountController`: الدخول والخروج وتغيير كلمة المرور؛ `RegistrationController`: التسجيل والتحقق؛ `PasswordResetController`: الاستعادة بتليجرام.
- `ErrorsController`: صفحات الأخطاء دون كشف تفاصيل الاستثناء.

## قوالب الواجهات

- `Views/Shared/_Document.cshtml`: المستند والـSEO والفوتر وشاشة التحميل.
- `Views/Shared/_Layout.cshtml`: القالب العام؛ `_SiteHeader.cshtml`: مكونات الهيدر المشتركة.
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml` و`_AdminNav.cshtml`: قالب الإدارة وقائمتها.
- `Areas/Member/Views/Shared/_MemberLayout.cshtml` و`_MemberNav.cshtml`: قالب المستخدم وقائمته.
- `Areas/*/Views/<Controller>/<Action>.cshtml`: محتوى كل صفحة؛ نماذجها في `Models`.
- `Resources/Labels.resx` للعربية و`Labels.en.resx` للإنجليزية.
- `wwwroot/css/site.css` للتنسيق، و`wwwroot/js/site.js` لتفاعلات القوائم والتحميل.

## Core: العضوية والدفع

- `MembershipModels.cs`: الجداول والحالات؛ `MembershipSchema.cs`: الفهارس والعلاقات والقيم الأولية.
- `MembershipRules.cs`: حساب المبالغ والمطابقة وتسلسل الفترات وطريقة عرض التجديد.
- `MembershipService.cs`: منح الاشتراك والتأكيد والتعليق والتجميد والتدقيق. المعاملات هنا تتولى منع تكرار المنح.
- `MemberVerification.cs`: إنشاء الرمز والتحقق منه وربط الهاتف والحساب. لا يحفظ الرمز الصريح.
- `TelegramClient.cs`: إرسال HTTP إلى Telegram؛ `TelegramCommands.cs`: معالجة الأوامر والتحقق من صاحب الطلب.
- `MemberNotifications.cs`: جمهور التنبيه والتذكيرات وطابور التسليم وإعادة فحص الحقوق قبل الإرسال.
- `PaymentGateways.cs`: عقد موصل الدفع وسجل البوابات والتحقق من العملية. لا توجد بوابة حية جاهزة بمجرد وضع مفتاح.
- `Database.cs`: DbSets والحفظ وحدث أول نشر، مع بقاء قواعد الاستخراج السابقة.
- `Migrations`: مخطط القاعدة؛ لا تحرر migration مطبقة على بيانات فعلية، بل أنشئ أخرى.

## Worker والإعدادات والفحوص

- `Worker/Program.cs`: التسجيل والتشغيل؛ `MembershipWorker.cs`: معالجة البوت والرسائل والاستعلامات المالية بصورة مستقلة عن الجمع.
- `appsettings.json` في Web وWorker: الإعدادات التقليدية. القيم السرية الجديدة `***` للاستبدال اليدوي دون رفع القيم الحقيقية.
- `tests/HiringPs.Checks/MembershipChecks.cs`: قواعد وHTTP دون SQL.
- `MembershipShellFixture.cs`: بيانات وحسابات مصطنعة للمراجعة داخل مشروع الفحوص فقط؛ ليست جزءًا من Web المنشور.
- `MembershipSqlChecks.cs`: فحص اختياري في قاعدة SQL مؤقتة منفصلة.

متطلبات التشغيل والحدود الحالية موثقة في [MEMBERSHIP-SETUP.md](MEMBERSHIP-SETUP.md). تفاصيل الجمع والتواريخ في [PHASE1-WORKFLOW.md](PHASE1-WORKFLOW.md)، وPDF في [PDF-TEXT.md](PDF-TEXT.md).
