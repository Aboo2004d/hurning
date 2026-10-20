# تنظيم المشروع وقواعد جودة الكود

الموقع والعامل تطبيقان يستخدمان مكتبة Core مشتركة. فصل لحظة وجوال باي وتليجرام والمتصفح وPDF وAI والنشر يتم داخل المكتبة بالخدمات وحقن الاعتماديات؛ لا تحتاج هذه المجالات إلى تطبيقات مستقلة أو اتصالات شبكة داخلية.

## أين أضع التعديل؟

```text
src/HiringPs.Core/
  Features/
    Payments/
      Shared/{Contracts,Dtos,Exceptions,Services}
      Lahza/{DependencyInjection,Services}
      JawwalPay/{DependencyInjection,Dtos,Exceptions,Rules,Services}
    Telegram/{Exceptions,Services}
    Membership/{Dtos,Enums,Rules,Services}
    Collection/{Dtos,Enums,Exceptions,Rules,Services}
    Pdf/{Dtos,Exceptions,Rules,Services}
    Ai/{Dtos,Enums,Exceptions,Rules,Services}
    SocialPublishing/
      Shared/{DependencyInjection,Dtos,Enums,Rules,Services}
      Facebook/Services/
      LinkedIn/
        Shared/{Contracts,Services}
        Personal/Services/
        Organization/Services/
    Monitoring/{Dtos,Enums,Rules,Services}
    Opportunities/{Enums,Services}
    Locations/Services/
    Retention/{Rules,Services}
    Shared/{Rules,Services}
  Persistence/
    HiringDb.cs
    DesignTimeFactory.cs
    Entities/<المجال>/<اسم الكيان>.cs
    Configurations/<المجال>/<اسم الكيان>Configuration.cs
  Migrations/
src/HiringPs.Web/
  Models/<المجال>/{Inputs,ViewModels,Dtos}/
```

تظهر المجلدات عندما يوجد نوع فعلي يحتاجها؛ لا ننشئ خدمات أو واجهات أو مجلدات فارغة لمجرد ملء هذا المخطط. بقيت namespaces العامة الحالية ثابتة لحماية مراجع Web وWorker وRazor والكيانات وملفات migrations. نماذج الإدخال التي نُقلت من Controllers تحتفظ مؤقتًا بأسمائها المؤهلة السابقة للتوافق، مع وجود كل نموذج في ملف مستقل تحت Models.

راجع [خريطة نقل الملفات](REFACTORING-MAP.md) عند البحث عن ملف قديم مثل `Domain.cs` أو `Processing.cs` أو `MembershipModels.cs`.

## الحدود والمسؤوليات

- **Entity:** حالة مخزنة وهوية وعلاقات وقيود. كل كيان في ملف مستقل حسب مجاله تحت Persistence/Entities، مع `/// <summary>` لكل حقل يشرح غرضه، وبخاصة الحقول المالية والسرية والتزامن.
- **Configuration:** إعدادات EF Core المخصصة في `IEntityTypeConfiguration<T>` مستقل. يسجلها HiringDb صراحةً. الكيانات التي تكفيها conventions والـattributes لا تحتاج Configuration فارغًا. فلتر بيئة الاشتراك يبقى في DbContext لأنه يعتمد على نسخته الحالية.
- **DTO:** بيانات تعبر حد خدمة أو مزود؛ ليست كيان EF ولا تمنح الاستحقاق من تلقاء نفسها. استخدم records للنتائج الجديدة التي لا تتغير، ولا تضف سلوك دفع أو نشر إلى DTO. لا تغير أسماء حقول JSON لدى المزود ضمن إعادة تنظيم داخلية.
- **Input:** بيانات الإدخال المسموحة من المستخدم مع قيود التحقق. لا تربط Entity مباشرةً بنموذج تعديل يتيح تغيير حقول ليست من صلاحيات المستخدم.
- **ViewModel:** بيانات يحتاجها العرض. لا تجعل الـView تنفذ خصمًا أو تؤكد دفعة أو تتصل بمزود. بعض نماذج العرض خارج صفحات الدفع الحالية لا تزال تعرض كيانات للقراءة؛ تحويلها إلى إسقاطات DTO أصغر يتم مع مراجعة مسارات العرض المعنية، وليس بتغيير كل العقود دفعة واحدة.
- **Service:** قاعدة عمل أو تنسيق عملية. توثق دوالها العامة والخاصة بملخص يشرح الاستخدام والآثار المهمة. التعليقات تشرح سبب القيد وحدود العملية، ولا تدعي نجاحًا لم يتحقق.
- **Client/Adapter:** بروتوكول المزود واستجاباته ومهلته والتحقق من الحمولة. لا يمنح اشتراكًا لمجرد نجاح HTTP. عملاء LahzaApiClient وJawwalPayApiClient لا يصلون إلى EF؛ تستخدم المحولات حد IPaymentGatewayData لقراءة حقائق المشتري والمحفظة وحفظ الإيصال، بتنفيذ منفصل تحت Persistence/Services.

لا نضيف Repository عامًا يخفي EF أو واجهة لكل كلاس دون حاجة. نستخدم العقد عندما توجد تطبيقات مختلفة فعلًا، مثل IPaymentGateway وILinkedInDestination.

## المدفوعات

`IPaymentGateway` هو العقد المشترك، و`LahzaGateway` و`JawwalPayGateway` محولان مستقلان. DTOs مثل GatewayCheck وWalletReceipt في ملفاتها الخاصة. `PaymentReconciliation` يطابق تأكيد المزود بالمبلغ والعملة والمرجع والبيئة ثم يستدعي منح الاشتراك. `WalletCheckoutService` يدير حجز جلسة OTP والتأكيد ومنع تكرار الخصم. تسجيل HTTP منفصل تحت DependencyInjection، والتشخيص الآمن في PaymentTraceWriter.

`HostedPaymentCheckout` يملك منطق حجز بدء الدفع قبل اتصال المزود ويعيد PaymentCheckoutResult وPaymentCheckoutRoute دون MVC. `PaymentReviewService` يملك طلب المراجعة والموافقة اليدوية وحسم المراجعة وصلاحية الوصول إلى اسم المرفق، وتستدعيه الواجهات بعد التفويض. `LahzaWebhookService` يتحقق من التوقيع ويجدول تحققًا من API؛ Controller مسؤول عن نقل HTTP وحد الحجم وتطبيع رؤوس التوقيع فقط.

`PaymentReadService` وPaymentOrderProjection يعيدان DTOs لطلبات الدفع ومراجعاتها وآثارها وإيصالاتها. صفحات دفع العضو والإدارة والملخص المشترك لم تعد تستقبل PaymentOrder أو PaymentReview أو AuditEntry. WalletPaymentPageService تحت Web/Services/Payments يجمع نموذج المحفظة من DTOs مع هاتف مقنع ودون رموز جلسة. PaymentActionRules سياسة مشتركة بين العرض وطلب المراجعة؛ ليست بديلًا عن تحقق الأمر نفسه من الملكية والحالة.

تحتفظ محولات البوابات بتوافق constructors القديمة للمستدعين المباشرين، بينما يحدد ActivatorUtilitiesConstructor مسار الإنتاج الذي يحقن IPaymentGatewayData. تسجيل AddLahza وAddJawwalPay يضيف التنفيذ الافتراضي مرة واحدة باستخدام TryAddScoped.

لا تخلط فتح مبيعات جديدة بإمكان التحقق من دفعة سابقة. لا تؤكد دفعة من redirect المتصفح، ولا تكرر تأكيد محفظة مجهول النتيجة. حافظ على عزل Sandbox وProduction وعلى الأقفال والفهارس الفريدة.

## النشر والتكاملات الأخرى

`SocialPublishing` يقرأ الربط ويفك الرمز ويطبق النتيجة على محاولة النشر. اتصال Facebook موجود في FacebookPublishingClient، واتصال LinkedIn في LinkedInPublishingClient. اختلاف الهوية والصلاحية معزول في LinkedInPersonalDestination وLinkedInOrganizationDestination عبر ILinkedInDestination. يتبادل العملاء SocialPublishRequestDto وSocialPublishResultDto بدل كيان SocialPublication. النقل المشترك SocialProviderTransport لا يقرأ القاعدة ولا ينفذ إعادة نشر تلقائية.

تليجرام يفصل TelegramClient عن TelegramCommands وMemberNotifications. المتصفح يفصل BrowserSettings وBrowserNavigation وBrowserCollector، وPDF يفصل PdfTools عن PdfTextReader وPdfTextFiles. AI يفصل عميل AiExtractor عن تسجيل الاستخدام وسياسة الأدلة ونسخ التعليمات. حافظ على هذه الحدود عند إضافة وظيفة جديدة.

## الجمع والفرص وأوامر النشر

`CollectionPipeline` ينسق دورة الجمع وحياة قفل SQL والنطاقات. `SourceItemProcessingService` يعالج عنصرًا واحدًا: جلب النص، إعادة استخدام الاستخراج السابق، تشغيل AI، وسياسة المراجعة وإعادة المحاولة. تبقى ملكية القفل عند المنسق، ويمرر التحقق منه كدالة إلى المعالج قبل نقاط الحفظ الحالية. كل عنصر يستخدم نطاق DI مستقل كما كان.

`DiscoveryBatchStore` تحت Persistence/Services يحفظ عناصر صفحة الاكتشاف ومؤشر استئنافها وعداد الجولة داخل معاملة واحدة ضمن نطاق الصفحة المستقل. يعيد المنسق تحميل المؤشر والجولة بعد نجاح الحفظ؛ لا يمكن لحفظ خطأ في دورة الجمع تثبيت نصف صفحة فشلت.

`OpportunityValidationRules` يملك قواعد التحقق النقية من الحقول والموقع وروابط التقديم والتواريخ والتشابه. `OpportunityExtractionService` يطبق الاستخراج داخل معاملته ويحمي تعديلات الإدارة ويثبت أدلة التواريخ. `OpportunityService` يبقي الواجهة العامة السابقة للمستدعين ويفوض هذه المسؤوليات، مع أرشفة المنتهي. لا يعني صغر الواجهة اختفاء تعقيد التطبيق؛ خدمة الاستخراج ما زالت مرشحًا لتجزئة خطواتها لاحقًا.

`SocialPublicationService` يملك الحجز بقفل SQL، وفحص القالب والربط والتكرار، ثم إرسال المحاولة وتسجيل النتيجة بميزانية مستقلة عن انقطاع المتصفح. يتبادل SocialPublicationCommandDto وSocialPublicationCommandResultDto وSocialPublicationRoute دون MVC أو TempData. يحتفظ SocialController بالتحقق من نموذج HTTP واختيار الوجهات وتجميع الرسائل والعرض؛ ولا ينفذ الحجز أو الاتصال بالمزود بنفسه.

`SocialComposeReadService` يجهز قالب المنشور وسجله ووجهاته. تستقبل الواجهة SocialOpportunitySummaryDto وSocialDestinationSummaryDto وSocialPublicationSummaryDto؛ بيانات الوجهة تقتصر على المزود والاسم والتفعيل والانتهاء، دون رمز الوصول المشفر. `SocialPublishInput` منفصل تحت Web/Models/SocialPublishing/Inputs ويحمل حقول الأمر فقط. التحقق من أهلية الإرسال داخل SocialPublicationService يبقى لازمًا ولا يعتمد على حالة العرض.

`SubscriptionReadService` يسقط اشتراكات الإدارة إلى SubscriptionSummaryDto وأسماء النسخ إلى MembershipPlanLabelDto، مع الحفاظ على فلتر بيئة الدفع والبحث والحالات والترتيب قبل الترقيم. `SubscriptionManagementService` ينفذ الإيقاف داخل معاملة Serializable وقفل العضو ويسجل السبب؛ Controller يبقى مسؤولًا عن التفويض وHTTP.

`MemberPlanReadService` يعيد كتالوج الباقات حسب ترتيبها وفترات استحقاق العضو ونسخة الباقة المرئية إلى MembershipPlanVersionSummaryDto وMemberSubscriptionPeriodDto. كتالوج الباقات يربط النسخ الحالية، بينما فترات الاستحقاق تربط النسخ المشتراة الأصلية. `SubscriptionPeriodDisplayRules` يجمع الفترات المتصلة لنفس النسخة للعرض دون تغيير المدخلات أو السجلات.

`MembershipDiscountService` يوحد شروط الخصم بين المعاينة والحجز. `MembershipCheckoutService` يقرأ شروط النسخة من القاعدة، ثم يقفل العضو داخل Serializable ويعيد التحقق من النسخة الحالية ويحجز الخصم وينشئ الطلب بمبلغه وبيئته. إذا تغير مبلغ الخصم بين المعاينة والحجز يعيد PlanChanged دون إنشاء طلب، وتعيد الواجهة عرض السعر للموافقة. التأكيد المجاني يعتمد على المبلغ المحجوز فعليًا. الطلب المعاد استخدامه يجب أن ينتمي للعضو وبيئة الدفع الحالية. لا يحدث هذا المسار مبالغ الطلبات القائمة.

حقول Plan وGateways وFinalAmount وStartsAt في PurchaseInput بيانات خادم عليها BindNever؛ PresentedVersionId وPresentedAmount يمثلان ما عُرض للعضو ويتحقق منهما Controller قبل تفويض الأمر. الخدمة لا تعتمد على كيان خطة وارد من المتصفح ولا تتعامل مع ModelState أو MVC.

## إدارة الباقات والمصادر والفرص

`PlanManagementService` يحفظ نسخة الباقة ضمن معاملة ويحافظ على rowversion. `AdminPlanReadService` يعيد DTOs للقائمة والمحرر، و`OpportunityAccessService` يفصل قواعد إتاحة التفاصيل. `DiscountManagementService` يفصل قائمة الخصومات وحفظها ويحافظ على منع تغيير شروط الخصومات المستخدمة. اختيارات الباقات للخصم تشمل النسخ الحالية غير المرئية أيضًا كما كانت سابقًا.

`AdminMemberReadService` يعيد حقول العضو والسجل للإدارة دون معرفات Telegram الداخلية، و`MemberRecoveryService` ينسق إبطال الربط القديم وإنشاء تحدي استعادة جديد. بناء الرابط والتفويض ورسائل HTTP يبقى في Controller.

`SourceReadService` يسقط قائمة المصادر وعدادات الاستئناف إلى DTOs. `SourceManagementService` يفصل تحقق URL والموصل والإعدادات وتطبيق الجدولة وحماية التزامن. `SourceCollectionRequestService` يملك حجز طلب الجمع اليدوي. `SourceInput` منفصل عن Source وSourceEditDto؛ الحقول المسموحة في النموذج لم تتوسع.

`AdminOpportunityReadService` يفصل قوائم الفرص وعداداتها. `OpportunityManagementService` يملك حفظ اعتماد الإدارة وبصمة الفرصة وروابط الإدخال اليدوي واعتماد المواقع وتحديث حالات النصوص الأصلية. `OpportunityInput` يفصل النموذج عن Opportunity وOpportunityEditDto. `SocialTemplateManagementService` يحفظ نسخ قوالب النشر تحت قفل SQL ويعيد SocialTemplateSummaryDto للسجل، وشاشة التكاملات تستقبل SocialIntegrationSummaryDto دون رمز الوصول المشفر.

## الأسعار المباشرة وتسوية المدفوعات

راجع [تحديث الأسعار](LIVE-PLAN-PRICES.md) و[تسوية المدفوعات](PAYMENT-RECOVERY.md) للسلوك والحدود. إشعارات الأسعار ترسل بعد نجاح معاملة تعديل الباقة. الكتالوج ومعاينة ما قبل الشراء يعيدان القراءة من الخادم؛ جلسات الدفع القائمة وفترات الاشتراك المشتراة لا تشترك في تحديث الأسعار.

PaymentRecoveryService يختار دفعات لحظة التي بدأت، وتأكيدات جوال باي التي حُجزت، بما فيها الفشل السابق. MembershipWorker يعزل كل تحقق داخل نطاق مستقل. لا تعاد أي عملية خصم، ولا يوجد افتراض بأن غياب الإيصال يعني فشل الدفع.

## حدود النطاق والتحقق

تم فصل المسارات المالية والعضوية والنشر ومحررات المصادر والفرص المرتبطة بهذه الدفعة. ما زالت بعض صفحات المحتوى والتشخيص البسيطة تستعمل كيانات قراءة أو تنسق استعلامات داخل Controller؛ لا ندعي أن كل شاشة في المستودع أصبحت DTO أو أن وجود Controller قصير وحده يثبت جودة التصميم. يمكن تطبيق نفس الحدود عند تطوير تلك المسارات دون تحويل آلي يغير عقودها.

الرموز النصية المحفوظة تبقى بعقودها الحالية؛ استبدالها بـ enum رقمي ليس شرطًا للنظافة ويتطلب خطة توافق إذا تغير التخزين. خدمة تطبيق الاستخراج ما زالت طويلة لأن قواعد الأدلة والمعاملة مترابطة؛ فصلها إضافيًا قرار مستقل، وليس نسخها إلى partial يخفي الحجم فقط.

البناء والتحقق الوظيفي والتكاملات لم تُشغّل بطلب المستخدم. لا تثبت المراجعة المصدرية صلاحية C# أو ترجمة استعلامات EF أو سلوك المتصفح وقت التشغيل. لم تُعدّل الأسرار أو ملفات إعدادات التطبيق أو migrations، ولم تنشأ commits أو push لهذه الدفعة.

## Enums وقاعدة البيانات

قيم enums الحالية أصبحت رقمية صريحة بالقيم السابقة نفسها. أضف القيم الجديدة في النهاية ولا تعيد ترقيم بيانات محفوظة. `SocialPublicationOutcome` يصف نتائج العملاء الجديدة ويحوّل إلى الرموز النصية السابقة عند حفظ النشر.

بعض الحالات ورموز النتائج النصية الحالية هي عقود تخزين أو ترجمة. لا تحولها إلى enum مخزن رقميًا دون خطة توافق وmigration مناسبة. لم تعدّل migrations أو ملفات snapshot في إعادة التنظيم هذه، ولم تُشغّل عمليات تحديث قاعدة بيانات. توثيق C# لا يضيف وصفًا إلى الأعمدة داخل SQL Server؛ ذلك تغيير schema مستقل عند الحاجة.

## معايير المراجعة

1. كلاس أو enum أو DTO عام واحد في الملف، والاسم مطابق لنوعه؛ ملفات partial مترابطة تبقى بجوار بعضها، ويمكن إبقاء حالة داخلية خاصة داخل الكلاس الذي يملكها.
2. أسماء واضحة، ودالة ذات مسؤولية مفهومة، و`/// <summary>` يذكر الغرض والقيد المهم. تجنب التعليقات التي تكرر الاسم دون معنى أو تشرح تفاصيل لم يعد الكود ينفذها.
3. الحفظ والتأثير الخارجي لهما حدود واضحة، مع احترام الإلغاء والمهلة وعدم تكرار أوامر الخصم والنشر مجهولة النتيجة.
4. لا أسرار أو OTP أو ردود مزود كاملة في السجلات؛ سجل معرّف المحاولة والمرحلة ورمز الخطأ فقط.
5. افصل تعديل التنظيم عن تغيير السلوك. حافظ على المراجع والأسماء وعقود JSON وقيم enums والأقفال والفهارس، وحدّث دليل المسارات عند النقل.
6. Controllers الحالية ما زالت تحتوي بعض منطق التنسيق والاستعلامات؛ فصلها إلى خدمات تطبيقية أصغر يتم لكل مسار بمعزل عن نقله إلى مجلد آخر. لا يُعد نقل الملفات وحده إثباتًا لاكتمال كل جوانب Clean Code.

## حدود التحقق الحالي

راجعت عملية النقل بمقارنة الرموز البرمجية للأنواع التي لم تتغير عمدًا، وبمطابقة تعليمات EF المنقولة إلى Configurations. لا تمثل هذه المقارنة تحققًا من نموذج EF وقت التشغيل أو من تكاملات الدفع والنشر. لم يُشغّل البناء أو الاختبارات أو خدمات خارجية التزامًا بتوجيه المستخدم. يلزم التحقق الوظيفي قبل اعتماد جاهزية هذه الإعادة التنظيمية للنشر.
