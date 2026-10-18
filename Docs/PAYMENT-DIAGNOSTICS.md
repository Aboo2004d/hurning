# تشخيص تحقق الدفع

## بعد تحديث الكود محليًا

أوقف أي `dotnet watch` أو عمليات Web/Worker قديمة. من مجلد Hire نفّذ:

```powershell
.\scripts\stop-local.ps1
.\scripts\start-local.ps1
```

لا تستخدم `-NoBuild` لهذه التجربة. أبقِ ngrok يعمل على منفذ Web الصحيح. السكربت يعيد بناء وتشغيل المشروعين؛ تعديل إعدادات Web وحده لا يحدّث إعدادات Worker المنسوخة إلى bin.

## جمع التشخيص دون دفع جديد

1. من الطلب نفسه اضغط «التحقق من الدفع» مرة واحدة. انتظر 30 ثانية بين الطلبات المتتالية.
2. يظهر رقم تتبع آمن تحت ملخص الطلب. أرسل هذا الرقم، وليس المفتاح أو رد المزود الكامل.
3. افتح الإدارة ← المدفوعات ← تفاصيل الطلب نفسه ← تتبع التحقق من الدفع. تظهر آخر 10 محاولات محفوظة. كل محاولة تتضمن التطبيق Web أوWorker، بيئة التشغيل والطلب والمحول، مراحل التحقق ورمز سبب الرفض وHTTP عند توفره.
4. الإصدار المتوقع لهذه الدفعة: `payment-trace-wallet-receipt-v4`. يطبعه كل تطبيق عند التحقق. غياب لوحة التشخيص يعني أن Web لم يُحدّث؛ عدم وجود أسطر من Worker لا يثبت وحده توقفه، فقد لا يوجد طلب مستحق للفحص.
5. إذا تعذر حفظ التشخيص أو لم تصل العملية لمرحلة العرض، افحص السجلات:

```powershell
Get-Content .\artifacts\local-logs\web.out.log -Tail 200 | Select-String 'PaymentTrace|PaymentWebhook'
Get-Content .\artifacts\local-logs\worker.out.log -Tail 200 | Select-String 'PaymentTrace|PaymentWebhook'
```

التتبع يسجل رموزًا ثابتة فقط، ولا يسجل التوكن أو المفتاح أو OTP أو أرقام الهاتف أو بيانات البطاقة أو جسم رد المزود أو رسالة الاستثناء. فشل التخزين التشخيصي يسجل `StorageUnavailable` ولا يغيّر نتيجة الدفع. يستخدم سياق SQL منفصلًا كي لا يحفظ تغييرات اشتراك جزئية. المحاولات المقيدة بمهلة أو المدفوعة مسبقًا تسجل في سجلات التطبيق دون تكرار إدخالات قاعدة البيانات.

## تفسير أهم الرموز

| الرمز | المعنى |
|---|---|
| MissingSecret | مفتاح البيئة الحالية ناقص أو ما زال *** |
| WrongSecretEnvironment | بادئة المفتاح لا تطابق Test/Live لبيئة التشغيل |
| InvalidPublicOrigin | أصل الموقع غير صالح؛ يجب HTTPS دون /ar أو /en |
| OrderEnvironmentMismatch | الطلب من Sandbox والتطبيق Production أو العكس |
| StoredReferenceMismatch | المرجع المحفوظ لا يطابق رقم الطلب |
| HttpFailure | رد API برمز فشل؛ يظهر رقم HTTP مثل 401 أو 403 |
| NetworkFailure / Timeout | الاتصال أو المهلة، دون افتراض فشل الخصم |
| ApiRejected | HTTP نجح لكن status العام لم يكن true |
| AmountInvalidString / AmountTypeNull / AmountInvalidAgora | المبلغ غير قابل للقراءة أو لا يمثل عدد أغورات صحيحًا؛ النص الرقمي مثل "500" مقبول بالفاصلة الثابتة |
| ReferenceMissing / CurrencyTypeNull / StatusTypeTrue | اسم الحقل ونوعه أو غيابه يحددان سبب فشل قراءة الرد دون حفظ قيمه |
| MalformedJson / MalformedVerificationData | تعذّر قراءة شكل الرد المتوقع |
| AmountMismatch / CurrencyMismatch / ReferenceMismatch | بيانات التحقق لا تطابق الطلب |
| ResponseEnvironmentMismatch | domain مذكور صراحة بقيمة تخالف البيئة؛ غيابه وفراغه مقبولان |
| Provider: Confirmed ثم Subscription: SaveStarted ثم Failure | API أكد الدفع، والخطأ في اعتماد الاشتراك؛ رقم SqlError إن توفر يساعد تشخيص SQL |
| Complete: SubscriptionSaved | تم اعتماد الاشتراك وحفظه |
| Wait30Seconds / ConcurrentCheck | فحص متقارب أو متزامن؛ لا تعاود الدفع |

## Webhook مقابل التحقق اليدوي

زر التحقق يستعلم من API مباشرة ولا يحتاج Webhook. وجود دفعة في لوحة لحظة لا يكفي وحده لتفعيل الاشتراك محليًا.

للتأكيد التلقائي، يرسل لحظة إلى `https://<origin>/integrations/lahza/webhook` دون بادئة لغة. ابحث في ngrok عن هذا الطلب:

- 401: إعداد غير جاهز أو توقيع غير صحيح؛ سجل `PaymentWebhook` يميز السبب.
- 200 مع `Accepted`: وصل حدث charge.success الموقّع وحُفظ طلب التحقق. `MatchedOrders=0` قد يعني مرجعًا غير موجود، بيئة مختلفة، أو طلبًا مدفوعًا بالفعل. لا يعني 200 وحده تفعيل الاشتراك؛ Worker يستعلم من API.
- عدم وجود الطلب: راجع رابط Test Webhook في لوحة لحظة واستمرار ngrok. لا يفسر ذلك فشل زر التحقق اليدوي.

لا يحتاج هذا التتبع Migration؛ يستخدم سجل Audit الموجود. عرض التشخيص المفصل محصور بالإدارة، والعضو يرى رقم التتبع فقط.

## حدود التحقق في هذه الدفعة

نجح بناء الحل و311 فحصًا محليًا و8 حالات عرض عبر Chromium. اختبارات مزود الدفع تستخدم HTTP محاكى، واختبار فشل حفظ التشخيص يثبت عدم تسريب رسالة الاستثناء وعدم تعطيل المسار. لم تتوفر قاعدة SQL حية لاختبار كتابة Audit أو مفاتيح لإعادة فحص دفعة المستخدم أو Webhook حي في البيئة السحابية.

## تتبع جوال باي

التتبع يشمل الآن Initiate وConfirm، وليس فحص الحالة وحده. عند تجربة Sandbox جديدة، ابحث عن محاولة فيها `WalletConfirm: RequestStarted` ثم `Session: ClaimCommitted` و`ProviderConfirm: RequestSent`.

- `ProviderFailure` مع HTTP يميز رفض المزود أو تعذر الاتصال.
- `WalletField.<field>.Missing` أو `Invalid<type>` يحدد حقل الإيصال المفقود أو المعيب. لا تُحفظ قيم الرد.
- `ProviderConfirm: ResponseParsed` ثم `Receipt: Saved` يعني أن الإيصال قرئ وحُفظ؛ ما بعده يخص تفعيل الاشتراك.
- `Complete: WalletAlreadyConfirming` يمنع تكرار تأكيد نفس الجلسة؛ لا يعاد إرسال confirm تلقائيًا أو يدويًا من زر فحص الحالة.
- `WalletSandboxReceiptMissing` في فحص الحالة يعني أن الجلسة بدأت التأكيد دون إيصال صالح محفوظ. لا يتم استدعاء قائمة العمليات الحقيقية لتفسير جلسة Sandbox، ولا يوجد endpoint تجريبي لقائمة العمليات في العقد المتاح.

التعديل لا يستعيد ردًا قديمًا لم يُحفظ. لتجربة التشخيص الجديد استخدم طلب Sandbox جديدًا فقط، ثم شارك آخر محاولة `WalletConfirm` من تفاصيل الطلب. في الإنتاج لا تبدأ خصمًا جديدًا عند غموض تأكيد سابق؛ استخدم الاستعلام المتاح أو مراجعة الدعم/الإدارة.

### رفض شرط مالي في الإيصال

إذا نجحت قراءة جميع الحقول ثم خالف الإيصال شروط الاعتماد، يعرض إصدار v4 رمزًا محددًا بدل WalletReceiptMismatch: `WalletTransactionIdInvalid`, `WalletAmountMismatch`, `WalletCurrencyMismatch`, `WalletReferenceTooLong`, `WalletJawwalFeeInvalid`, `WalletPlatformFeeInvalid`, `WalletNetAmountInvalid`, `WalletFeeSumMismatch`.

يحتوي سجل الأدمن على ReceiptFacts بالأرقام فقط: TransactionId، ExpectedAmountIls، ReceivedAmountIls، JawwalFeeIls، PlatformFeeIls، NetAmountIls. لا يحتوي على المرجع النصي أو الهاتف أو OTP أو مفتاح الجلسة أو جسم الرد. الرسوم + الصافي يجب أن تساوي المبلغ ضمن هامش الأغورة الموجود سابقًا؛ لا يغيّر التشخيص شروط القبول. لا تسجل الأرقام في رسالة المستخدم العامة.

لا يمكن استخراج هذه الحقائق بأثر رجعي من سجل v3 العام؛ يلزم رد تجربة Sandbox جديدة بعد التحديث. لا تعاود confirm للجلسة السابقة.

تُحفظ الحقائق الرقمية أيضًا عند قراءة إيصال صالح، حتى يمكن تشخيص فشل حفظه في SQL أو اعتماد الاشتراك بعده دون إعادة confirm.
