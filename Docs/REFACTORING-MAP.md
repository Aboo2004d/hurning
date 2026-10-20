# خريطة نقل ملفات المشروع

هذه الخريطة تربط الملفات الجامعة القديمة بالأنواع المفصولة. بقيت أسماء الأنواع وnamespaces الحالية متوافقة.

| الملف السابق | النوع | المسار الحالي |
| --- | --- | --- |
| `src/HiringPs.Core/Attachments.cs` | `AttachmentReader` | [AttachmentReader.cs](../src/HiringPs.Core/Features/Pdf/Services/AttachmentReader.cs) |
| `src/HiringPs.Core/Attachments.cs` | `ContentBundle` | [ContentBundle.cs](../src/HiringPs.Core/Features/Pdf/Dtos/ContentBundle.cs) |
| `src/HiringPs.Core/Attachments.cs` | `ContentNeedsReviewException` | [ContentNeedsReviewException.cs](../src/HiringPs.Core/Features/Pdf/Exceptions/ContentNeedsReviewException.cs) |
| `src/HiringPs.Core/AutomaticSource.cs` | `AutomaticSource` | [AutomaticSource.cs](../src/HiringPs.Core/Features/Collection/Services/AutomaticSource.cs) |
| `src/HiringPs.Core/BrowserNavigation.cs` | `BrowserNavigation` | [BrowserNavigation.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserNavigation.cs) |
| `src/HiringPs.Core/BrowserSettings.cs` | `BrowserSettings` | [BrowserSettings.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserSettings.cs) |
| `src/HiringPs.Core/Collection.cs` | `BrowserCollector` | [BrowserCollector.Collection.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserCollector.Collection.cs) |
| `src/HiringPs.Core/Collection.cs` | `SourceConfiguration` | [SourceConfiguration.cs](../src/HiringPs.Core/Features/Collection/Dtos/SourceConfiguration.cs) |
| `src/HiringPs.Core/Collection.cs` | `SqlRunLock` | [SqlRunLock.cs](../src/HiringPs.Core/Features/Collection/Services/SqlRunLock.cs) |
| `src/HiringPs.Core/Collection.cs` | `UrlPolicy` | [UrlPolicy.cs](../src/HiringPs.Core/Features/Collection/Rules/UrlPolicy.cs) |
| `src/HiringPs.Core/CollectorDiagnostics.cs` | `CollectorDiagnostic` | [CollectorDiagnostic.cs](../src/HiringPs.Core/Features/Collection/Dtos/CollectorDiagnostic.cs) |
| `src/HiringPs.Core/CollectorDiagnostics.cs` | `CollectorDiagnosticException` | [CollectorDiagnosticException.cs](../src/HiringPs.Core/Features/Collection/Exceptions/CollectorDiagnosticException.cs) |
| `src/HiringPs.Core/CollectorDiagnostics.cs` | `CollectorDiagnosticFiles` | [CollectorDiagnosticFiles.cs](../src/HiringPs.Core/Features/Collection/Services/CollectorDiagnosticFiles.cs) |
| `src/HiringPs.Core/CollectorDiagnostics.cs` | `CollectorDiagnosticSession` | [CollectorDiagnosticSession.cs](../src/HiringPs.Core/Features/Collection/Services/CollectorDiagnosticSession.cs) |
| `src/HiringPs.Core/CollectorDiagnostics.cs` | `NetworkDiagnostic` | [NetworkDiagnostic.cs](../src/HiringPs.Core/Features/Collection/Dtos/NetworkDiagnostic.cs) |
| `src/HiringPs.Core/ContactMessage.cs` | `ContactMessage` | [ContactMessage.cs](../src/HiringPs.Core/Persistence/Entities/Contact/ContactMessage.cs) |
| `src/HiringPs.Core/Database.cs` | `HiringDb` | [HiringDb.cs](../src/HiringPs.Core/Persistence/HiringDb.cs) |
| `src/HiringPs.Core/DateEvidenceRules.cs` | `CheckedDates` | [CheckedDates.cs](../src/HiringPs.Core/Features/Ai/Dtos/CheckedDates.cs) |
| `src/HiringPs.Core/DateEvidenceRules.cs` | `DateEvidenceRules` | [DateEvidenceRules.cs](../src/HiringPs.Core/Features/Ai/Rules/DateEvidenceRules.cs) |
| `src/HiringPs.Core/DesignTimeFactory.cs` | `DesignTimeFactory` | [DesignTimeFactory.cs](../src/HiringPs.Core/Persistence/DesignTimeFactory.cs) |
| `src/HiringPs.Core/Domain.cs` | `AuditEntry` | [AuditEntry.cs](../src/HiringPs.Core/Persistence/Entities/Shared/AuditEntry.cs) |
| `src/HiringPs.Core/Domain.cs` | `ConnectorKind` | [ConnectorKind.cs](../src/HiringPs.Core/Features/Collection/Enums/ConnectorKind.cs) |
| `src/HiringPs.Core/Domain.cs` | `Opportunity` | [Opportunity.cs](../src/HiringPs.Core/Persistence/Entities/Opportunities/Opportunity.cs) |
| `src/HiringPs.Core/Domain.cs` | `OpportunityKind` | [OpportunityKind.cs](../src/HiringPs.Core/Features/Opportunities/Enums/OpportunityKind.cs) |
| `src/HiringPs.Core/Domain.cs` | `OpportunitySourceLink` | [OpportunitySourceLink.cs](../src/HiringPs.Core/Persistence/Entities/Opportunities/OpportunitySourceLink.cs) |
| `src/HiringPs.Core/Domain.cs` | `ProcessingStatus` | [ProcessingStatus.cs](../src/HiringPs.Core/Features/Collection/Enums/ProcessingStatus.cs) |
| `src/HiringPs.Core/Domain.cs` | `PublicationStatus` | [PublicationStatus.cs](../src/HiringPs.Core/Features/Opportunities/Enums/PublicationStatus.cs) |
| `src/HiringPs.Core/Domain.cs` | `ScrapeRun` | [ScrapeRun.cs](../src/HiringPs.Core/Persistence/Entities/Collection/ScrapeRun.cs) |
| `src/HiringPs.Core/Domain.cs` | `Source` | [Source.cs](../src/HiringPs.Core/Persistence/Entities/Collection/Source.cs) |
| `src/HiringPs.Core/Domain.cs` | `SourceCollectionRequest` | [SourceCollectionRequest.cs](../src/HiringPs.Core/Persistence/Entities/Collection/SourceCollectionRequest.cs) |
| `src/HiringPs.Core/Domain.cs` | `SourceItem` | [SourceItem.cs](../src/HiringPs.Core/Persistence/Entities/Collection/SourceItem.cs) |
| `src/HiringPs.Core/Domain.cs` | `SourceItemRevision` | [SourceItemRevision.cs](../src/HiringPs.Core/Persistence/Entities/Collection/SourceItemRevision.cs) |
| `src/HiringPs.Core/Domain.cs` | `TextRules` | [TextRules.cs](../src/HiringPs.Core/Features/Shared/Rules/TextRules.cs) |
| `src/HiringPs.Core/Domain.cs` | `UserReport` | [UserReport.cs](../src/HiringPs.Core/Persistence/Entities/Opportunities/UserReport.cs) |
| `src/HiringPs.Core/Domain.cs` | `WorkerHeartbeat` | [WorkerHeartbeat.cs](../src/HiringPs.Core/Persistence/Entities/Collection/WorkerHeartbeat.cs) |
| `src/HiringPs.Core/LocationCatalog.cs` | `LocationCatalog` | [LocationCatalog.cs](../src/HiringPs.Core/Features/Locations/Services/LocationCatalog.cs) |
| `src/HiringPs.Core/LocationSearch.cs` | `LocationSearch` | [LocationSearch.cs](../src/HiringPs.Core/Features/Locations/Services/LocationSearch.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `JawwalPayGateway` | [JawwalPayGateway.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Services/JawwalPayGateway.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `JawwalPayRegistration` | [JawwalPayRegistration.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/DependencyInjection/JawwalPayRegistration.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `JawwalPayRules` | [JawwalPayRules.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Rules/JawwalPayRules.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `WalletBalance` | [WalletBalance.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Dtos/WalletBalance.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `WalletOtpSession` | [WalletOtpSession.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Dtos/WalletOtpSession.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `WalletReceipt` | [WalletReceipt.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Dtos/WalletReceipt.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `WalletReceiptValidationFacts` | [WalletReceiptValidationFacts.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Dtos/WalletReceiptValidationFacts.cs) |
| `src/HiringPs.Core/Membership/JawwalPayGateway.cs` | `WalletRequestRejectedException` | [WalletRequestRejectedException.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Exceptions/WalletRequestRejectedException.cs) |
| `src/HiringPs.Core/Membership/LahzaGateway.cs` | `LahzaGateway` | [LahzaGateway.cs](../src/HiringPs.Core/Features/Payments/Lahza/Services/LahzaGateway.cs) |
| `src/HiringPs.Core/Membership/LahzaGateway.cs` | `LahzaRegistration` | [LahzaRegistration.cs](../src/HiringPs.Core/Features/Payments/Lahza/DependencyInjection/LahzaRegistration.cs) |
| `src/HiringPs.Core/Membership/LahzaGateway.cs` | `PaymentProviderException` | [PaymentProviderException.cs](../src/HiringPs.Core/Features/Payments/Shared/Exceptions/PaymentProviderException.cs) |
| `src/HiringPs.Core/Membership/MemberNotifications.cs` | `MemberNotifications` | [MemberNotifications.cs](../src/HiringPs.Core/Features/Telegram/Services/MemberNotifications.cs) |
| `src/HiringPs.Core/Membership/MemberVerification.cs` | `MemberVerification` | [MemberVerification.cs](../src/HiringPs.Core/Features/Membership/Services/MemberVerification.cs) |
| `src/HiringPs.Core/Membership/MemberVerification.cs` | `VerificationRequest` | [VerificationRequest.cs](../src/HiringPs.Core/Features/Membership/Dtos/VerificationRequest.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `ChallengePurpose` | [ChallengePurpose.cs](../src/HiringPs.Core/Features/Membership/Enums/ChallengePurpose.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `DiscountCode` | [DiscountCode.cs](../src/HiringPs.Core/Persistence/Entities/Payments/Shared/DiscountCode.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MemberChallenge` | [MemberChallenge.cs](../src/HiringPs.Core/Persistence/Entities/Membership/MemberChallenge.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MemberMessage` | [MemberMessage.cs](../src/HiringPs.Core/Persistence/Entities/Telegram/MemberMessage.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MemberMessageKind` | [MemberMessageKind.cs](../src/HiringPs.Core/Features/Telegram/Enums/MemberMessageKind.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MemberPreference` | [MemberPreference.cs](../src/HiringPs.Core/Persistence/Entities/Membership/MemberPreference.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MemberProfile` | [MemberProfile.cs](../src/HiringPs.Core/Persistence/Entities/Membership/MemberProfile.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MemberSubscription` | [MemberSubscription.cs](../src/HiringPs.Core/Persistence/Entities/Membership/MemberSubscription.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MembershipPlan` | [MembershipPlan.cs](../src/HiringPs.Core/Persistence/Entities/Membership/MembershipPlan.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MembershipPlanVersion` | [MembershipPlanVersion.cs](../src/HiringPs.Core/Persistence/Entities/Membership/MembershipPlanVersion.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `MembershipRuntimeState` | [MembershipRuntimeState.cs](../src/HiringPs.Core/Persistence/Entities/Membership/MembershipRuntimeState.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `OpportunityAccessRule` | [OpportunityAccessRule.cs](../src/HiringPs.Core/Persistence/Entities/Membership/OpportunityAccessRule.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `OpportunityPublication` | [OpportunityPublication.cs](../src/HiringPs.Core/Persistence/Entities/Membership/OpportunityPublication.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `PaymentGatewaySetting` | [PaymentGatewaySetting.cs](../src/HiringPs.Core/Persistence/Entities/Payments/Shared/PaymentGatewaySetting.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `PaymentOrder` | [PaymentOrder.cs](../src/HiringPs.Core/Persistence/Entities/Payments/Shared/PaymentOrder.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `PaymentReview` | [PaymentReview.cs](../src/HiringPs.Core/Persistence/Entities/Payments/Shared/PaymentReview.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `PaymentState` | [PaymentState.cs](../src/HiringPs.Core/Features/Payments/Shared/Enums/PaymentState.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `SavedOpportunity` | [SavedOpportunity.cs](../src/HiringPs.Core/Persistence/Entities/Membership/SavedOpportunity.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `TelegramInbox` | [TelegramInbox.cs](../src/HiringPs.Core/Persistence/Entities/Telegram/TelegramInbox.cs) |
| `src/HiringPs.Core/Membership/MembershipModels.cs` | `WalletPaymentSession` | [WalletPaymentSession.cs](../src/HiringPs.Core/Persistence/Entities/Payments/JawwalPay/WalletPaymentSession.cs) |
| `src/HiringPs.Core/Membership/MembershipRules.cs` | `MembershipRules` | [MembershipRules.cs](../src/HiringPs.Core/Features/Membership/Rules/MembershipRules.cs) |
| `src/HiringPs.Core/Membership/MembershipSchema.cs` | `MembershipSchema` | تعريفاته موزعة على [Persistence/Configurations](../src/HiringPs.Core/Persistence/Configurations) |
| `src/HiringPs.Core/Membership/MembershipService.cs` | `MembershipService` | [MembershipService.cs](../src/HiringPs.Core/Features/Membership/Services/MembershipService.cs) |
| `src/HiringPs.Core/Membership/PaymentEnvironment.cs` | `PaymentEnvironment` | [PaymentEnvironment.cs](../src/HiringPs.Core/Features/Payments/Shared/Services/PaymentEnvironment.cs) |
| `src/HiringPs.Core/Membership/PaymentGateways.cs` | `GatewayCheck` | [GatewayCheck.cs](../src/HiringPs.Core/Features/Payments/Shared/Dtos/GatewayCheck.cs) |
| `src/HiringPs.Core/Membership/PaymentGateways.cs` | `GatewayCheckout` | [GatewayCheckout.cs](../src/HiringPs.Core/Features/Payments/Shared/Dtos/GatewayCheckout.cs) |
| `src/HiringPs.Core/Membership/PaymentGateways.cs` | `IPaymentGateway` | [IPaymentGateway.cs](../src/HiringPs.Core/Features/Payments/Shared/Contracts/IPaymentGateway.cs) |
| `src/HiringPs.Core/Membership/PaymentGateways.cs` | `PaymentGatewayRegistry` | [PaymentGatewayRegistry.cs](../src/HiringPs.Core/Features/Payments/Shared/Services/PaymentGatewayRegistry.cs) |
| `src/HiringPs.Core/Membership/PaymentGateways.cs` | `PaymentReconciliation` | [PaymentReconciliation.cs](../src/HiringPs.Core/Features/Payments/Shared/Services/PaymentReconciliation.cs) |
| `src/HiringPs.Core/Membership/PaymentToolProbes.cs` | `PaymentToolProbes` | [PaymentToolProbes.cs](../src/HiringPs.Core/Features/Payments/Shared/Services/PaymentToolProbes.cs) |
| `src/HiringPs.Core/Membership/PaymentTraceWriter.cs` | `PaymentTraceSession` | [PaymentTraceSession.cs](../src/HiringPs.Core/Features/Payments/Shared/Services/PaymentTraceSession.cs) |
| `src/HiringPs.Core/Membership/PaymentTraceWriter.cs` | `PaymentTraceStep` | [PaymentTraceStep.cs](../src/HiringPs.Core/Features/Payments/Shared/Dtos/PaymentTraceStep.cs) |
| `src/HiringPs.Core/Membership/PaymentTraceWriter.cs` | `PaymentTraceWriter` | [PaymentTraceWriter.cs](../src/HiringPs.Core/Features/Payments/Shared/Services/PaymentTraceWriter.cs) |
| `src/HiringPs.Core/Membership/TelegramClient.cs` | `TelegramClient` | [TelegramClient.cs](../src/HiringPs.Core/Features/Telegram/Services/TelegramClient.cs) |
| `src/HiringPs.Core/Membership/TelegramClient.cs` | `TelegramDeliveryException` | [TelegramDeliveryException.cs](../src/HiringPs.Core/Features/Telegram/Exceptions/TelegramDeliveryException.cs) |
| `src/HiringPs.Core/Membership/TelegramCommands.cs` | `TelegramCommands` | [TelegramCommands.cs](../src/HiringPs.Core/Features/Telegram/Services/TelegramCommands.cs) |
| `src/HiringPs.Core/Membership/WalletCheckoutService.cs` | `WalletCheckoutService` | [WalletCheckoutService.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Services/WalletCheckoutService.cs) |
| `src/HiringPs.Core/MergeHistory.cs` | `MergeHistory` | [MergeHistory.cs](../src/HiringPs.Core/Features/Opportunities/Services/MergeHistory.cs) |
| `src/HiringPs.Core/OpportunityRss.cs` | `OpportunityRss` | [OpportunityRss.cs](../src/HiringPs.Core/Features/Opportunities/Services/OpportunityRss.cs) |
| `src/HiringPs.Core/PdfRetry.cs` | `PdfRetry` | [PdfRetry.cs](../src/HiringPs.Core/Features/Pdf/Services/PdfRetry.cs) |
| `src/HiringPs.Core/PdfTextFiles.cs` | `PdfPageReference` | [PdfPageReference.cs](../src/HiringPs.Core/Features/Pdf/Dtos/PdfPageReference.cs) |
| `src/HiringPs.Core/PdfTextFiles.cs` | `PdfPageText` | [PdfPageText.cs](../src/HiringPs.Core/Features/Pdf/Dtos/PdfPageText.cs) |
| `src/HiringPs.Core/PdfTextFiles.cs` | `PdfTextFiles` | [PdfTextFiles.cs](../src/HiringPs.Core/Features/Pdf/Services/PdfTextFiles.cs) |
| `src/HiringPs.Core/PdfTextFiles.cs` | `PdfTextResult` | [PdfTextResult.cs](../src/HiringPs.Core/Features/Pdf/Dtos/PdfTextResult.cs) |
| `src/HiringPs.Core/PdfTextReader.cs` | `PdfTextReader` | [PdfTextReader.cs](../src/HiringPs.Core/Features/Pdf/Services/PdfTextReader.cs) |
| `src/HiringPs.Core/PdfToolPaths.cs` | `PdfToolPaths` | [PdfToolPaths.cs](../src/HiringPs.Core/Features/Pdf/Rules/PdfToolPaths.cs) |
| `src/HiringPs.Core/PdfTools.cs` | `PdfToolFailureException` | [PdfToolFailureException.cs](../src/HiringPs.Core/Features/Pdf/Exceptions/PdfToolFailureException.cs) |
| `src/HiringPs.Core/PdfTools.cs` | `PdfTools` | [PdfTools.cs](../src/HiringPs.Core/Features/Pdf/Services/PdfTools.cs) |
| `src/HiringPs.Core/Processing.cs` | `AiExtractor` | [AiExtractor.cs](../src/HiringPs.Core/Features/Ai/Services/AiExtractor.cs) |
| `src/HiringPs.Core/Processing.cs` | `AiTransientException` | [AiTransientException.cs](../src/HiringPs.Core/Features/Ai/Exceptions/AiTransientException.cs) |
| `src/HiringPs.Core/Processing.cs` | `AiUnavailableException` | [AiUnavailableException.cs](../src/HiringPs.Core/Features/Ai/Exceptions/AiUnavailableException.cs) |
| `src/HiringPs.Core/Processing.cs` | `CollectionPipeline` | [CollectionPipeline.cs](../src/HiringPs.Core/Features/Collection/Services/CollectionPipeline.cs) |
| `src/HiringPs.Core/Processing.cs` | `ExtractedOpportunity` | [ExtractedOpportunity.cs](../src/HiringPs.Core/Features/Ai/Dtos/ExtractedOpportunity.cs) |
| `src/HiringPs.Core/Processing.cs` | `ExtractionResult` | [ExtractionResult.cs](../src/HiringPs.Core/Features/Ai/Dtos/ExtractionResult.cs) |
| `src/HiringPs.Core/Processing.cs` | `OpportunityService` | [OpportunityService.cs](../src/HiringPs.Core/Features/Opportunities/Services/OpportunityService.cs) |
| `src/HiringPs.Core/Processing.cs` | `Strings` | [Strings.cs](../src/HiringPs.Core/Features/Shared/Services/Strings.cs) |
| `src/HiringPs.Core/PromptDatePolicy.cs` | `PromptDatePolicy` | [PromptDatePolicy.cs](../src/HiringPs.Core/Features/Ai/Rules/PromptDatePolicy.cs) |
| `src/HiringPs.Core/PromptDefaults.cs` | `PromptDefaults` | [PromptDefaults.cs](../src/HiringPs.Core/Features/Ai/Rules/PromptDefaults.cs) |
| `src/HiringPs.Core/SitePage.cs` | `SitePage` | [SitePage.cs](../src/HiringPs.Core/Persistence/Entities/Content/SitePage.cs) |
| `src/HiringPs.Core/SocialPublishing.cs` | `SocialConnection` | [SocialConnection.cs](../src/HiringPs.Core/Persistence/Entities/SocialPublishing/Shared/SocialConnection.cs) |
| `src/HiringPs.Core/SocialPublishing.cs` | `SocialPublication` | [SocialPublication.cs](../src/HiringPs.Core/Persistence/Entities/SocialPublishing/Shared/SocialPublication.cs) |
| `src/HiringPs.Core/SocialPublishing.cs` | `SocialPublishing` | [SocialPublishing.cs](../src/HiringPs.Core/Features/SocialPublishing/Shared/Services/SocialPublishing.cs) |
| `src/HiringPs.Core/SocialRegistration.cs` | `SocialRegistration` | [SocialRegistration.cs](../src/HiringPs.Core/Features/SocialPublishing/Shared/DependencyInjection/SocialRegistration.cs) |
| `src/HiringPs.Core/SocialTemplates.cs` | `SocialTemplateText` | [SocialTemplateText.cs](../src/HiringPs.Core/Features/SocialPublishing/Shared/Rules/SocialTemplateText.cs) |
| `src/HiringPs.Core/SocialTemplates.cs` | `SocialTemplateVersion` | [SocialTemplateVersion.cs](../src/HiringPs.Core/Persistence/Entities/SocialPublishing/Shared/SocialTemplateVersion.cs) |
| `src/HiringPs.Core/SocialTemplates.cs` | `SocialTemplates` | [SocialTemplates.cs](../src/HiringPs.Core/Features/SocialPublishing/Shared/Services/SocialTemplates.cs) |
| `src/HiringPs.Core/SourceEvidence.cs` | `SourceEvidence` | [SourceEvidence.cs](../src/HiringPs.Core/Features/Collection/Services/SourceEvidence.cs) |
| `src/HiringPs.Core/SourceSchedule.cs` | `SourceSchedule` | [SourceSchedule.cs](../src/HiringPs.Core/Features/Collection/Rules/SourceSchedule.cs) |
| `src/HiringPs.Core/SourceSetupAssistant.cs` | `SourceSetupAssistant` | [SourceSetupAssistant.cs](../src/HiringPs.Core/Features/Collection/Services/SourceSetupAssistant.cs) |
| `src/HiringPs.Core/SourceSetupBrowser.cs` | `BrowserCollector` | [BrowserCollector.SourceSetupBrowser.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserCollector.SourceSetupBrowser.cs) |
| `src/HiringPs.Core/SourceSetupModels.cs` | `SourceSetupLink` | [SourceSetupLink.cs](../src/HiringPs.Core/Features/Collection/Dtos/SourceSetupLink.cs) |
| `src/HiringPs.Core/SourceSetupModels.cs` | `SourceSetupPreview` | [SourceSetupPreview.cs](../src/HiringPs.Core/Features/Collection/Dtos/SourceSetupPreview.cs) |
| `src/HiringPs.Core/SourceSetupModels.cs` | `SourceSetupResult` | [SourceSetupResult.cs](../src/HiringPs.Core/Features/Collection/Dtos/SourceSetupResult.cs) |
| `src/HiringPs.Core/SourceSetupModels.cs` | `SourceSetupRules` | [SourceSetupRules.cs](../src/HiringPs.Core/Features/Collection/Rules/SourceSetupRules.cs) |
| `src/HiringPs.Core/SourceSetupModels.cs` | `SourceSetupSnapshot` | [SourceSetupSnapshot.cs](../src/HiringPs.Core/Features/Collection/Dtos/SourceSetupSnapshot.cs) |
| `src/HiringPs.Core/SourceSetupModels.cs` | `SourceSetupSuggestion` | [SourceSetupSuggestion.cs](../src/HiringPs.Core/Features/Collection/Dtos/SourceSetupSuggestion.cs) |
| `src/HiringPs.Core/TextEvidence.cs` | `TextEvidence` | [TextEvidence.cs](../src/HiringPs.Core/Features/Shared/Services/TextEvidence.cs) |
| `src/HiringPs.Core/ToolCheckQueue.cs` | `ToolCheckQueue` | [ToolCheckQueue.cs](../src/HiringPs.Core/Features/Monitoring/Services/ToolCheckQueue.cs) |
| `src/HiringPs.Core/ToolCheckSchedule.cs` | `ToolCheckSchedule` | [ToolCheckSchedule.cs](../src/HiringPs.Core/Features/Monitoring/Rules/ToolCheckSchedule.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `CollectionRuntimeSnapshot` | [CollectionRuntimeSnapshot.cs](../src/HiringPs.Core/Features/Monitoring/Dtos/CollectionRuntimeSnapshot.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolCheckKind` | [ToolCheckKind.cs](../src/HiringPs.Core/Features/Monitoring/Enums/ToolCheckKind.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolCheckRequest` | [ToolCheckRequest.cs](../src/HiringPs.Core/Persistence/Entities/Monitoring/ToolCheckRequest.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolCheckResult` | [ToolCheckResult.cs](../src/HiringPs.Core/Persistence/Entities/Monitoring/ToolCheckResult.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolMonitoringRules` | [ToolMonitoringRules.cs](../src/HiringPs.Core/Features/Monitoring/Rules/ToolMonitoringRules.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolProbe` | [ToolProbe.cs](../src/HiringPs.Core/Features/Monitoring/Dtos/ToolProbe.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolRequestStatus` | [ToolRequestStatus.cs](../src/HiringPs.Core/Features/Monitoring/Enums/ToolRequestStatus.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolState` | [ToolState.cs](../src/HiringPs.Core/Features/Monitoring/Enums/ToolState.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolWorkerRuntime` | [ToolWorkerRuntime.cs](../src/HiringPs.Core/Features/Monitoring/Services/ToolWorkerRuntime.cs) |
| `src/HiringPs.Core/ToolMonitoringModels.cs` | `ToolWorkerState` | [ToolWorkerState.cs](../src/HiringPs.Core/Persistence/Entities/Monitoring/ToolWorkerState.cs) |
| `src/HiringPs.Core/ToolProbes.cs` | `ToolProbes` | [ToolProbes.cs](../src/HiringPs.Core/Features/Monitoring/Services/ToolProbes.cs) |
| `src/HiringPs.Core/UnCareersFeed.cs` | `UnCareersFeed` | [UnCareersFeed.cs](../src/HiringPs.Core/Features/Collection/Services/UnCareersFeed.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `AiExecution` | [AiExecution.cs](../src/HiringPs.Core/Features/Ai/Dtos/AiExecution.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `AiTelemetry` | [AiTelemetry.cs](../src/HiringPs.Core/Features/Ai/Dtos/AiTelemetry.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `AiUsage` | [AiUsage.cs](../src/HiringPs.Core/Persistence/Entities/Ai/AiUsage.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `DiscoveryBatch` | [DiscoveryBatch.cs](../src/HiringPs.Core/Features/Collection/Dtos/DiscoveryBatch.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `DiscoveryCheckpoint` | [DiscoveryCheckpoint.cs](../src/HiringPs.Core/Persistence/Entities/Collection/DiscoveryCheckpoint.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `ExtractedDate` | [ExtractedDate.cs](../src/HiringPs.Core/Features/Ai/Dtos/ExtractedDate.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `MergeOperation` | [MergeOperation.cs](../src/HiringPs.Core/Persistence/Entities/Opportunities/MergeOperation.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `OpportunityDateKind` | [OpportunityDateKind.cs](../src/HiringPs.Core/Features/Ai/Enums/OpportunityDateKind.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `OpportunityDateRecord` | [OpportunityDateRecord.cs](../src/HiringPs.Core/Persistence/Entities/Ai/OpportunityDateRecord.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `PromptVersion` | [PromptVersion.cs](../src/HiringPs.Core/Persistence/Entities/Ai/PromptVersion.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `RetentionPolicy` | [RetentionPolicy.cs](../src/HiringPs.Core/Persistence/Entities/Retention/RetentionPolicy.cs) |
| `src/HiringPs.Core/WorkflowModels.cs` | `WorkflowSettings` | [WorkflowSettings.cs](../src/HiringPs.Core/Persistence/Entities/Ai/WorkflowSettings.cs) |
| `src/HiringPs.Core/WorkflowServices.cs` | `AiUsageRecorder` | [AiUsageRecorder.cs](../src/HiringPs.Core/Features/Ai/Services/AiUsageRecorder.cs) |
| `src/HiringPs.Core/WorkflowServices.cs` | `PromptWorkflow` | [PromptWorkflow.cs](../src/HiringPs.Core/Features/Ai/Services/PromptWorkflow.cs) |
| `src/HiringPs.Core/WorkflowServices.cs` | `RetentionCleanup` | [RetentionCleanup.cs](../src/HiringPs.Core/Features/Retention/Services/RetentionCleanup.cs) |
| `src/HiringPs.Web/Models/AdminDashboard.cs` | `AdminDashboard` | [AdminDashboard.cs](../src/HiringPs.Web/Models/Dashboard/ViewModels/AdminDashboard.cs) |
| `src/HiringPs.Web/Models/AdminPagination.cs` | `AdminPage` | [AdminPage.cs](../src/HiringPs.Web/Models/Shared/ViewModels/AdminPage.cs) |
| `src/HiringPs.Web/Models/AdminPagination.cs` | `AdminPager` | [AdminPager.cs](../src/HiringPs.Web/Models/Shared/ViewModels/AdminPager.cs) |
| `src/HiringPs.Web/Models/AdminPagination.cs` | `AdminPagination` | [AdminPagination.cs](../src/HiringPs.Web/Models/Shared/ViewModels/AdminPagination.cs) |
| `src/HiringPs.Web/Models/ContactModels.cs` | `ContactInbox` | [ContactInbox.cs](../src/HiringPs.Web/Models/Contact/ViewModels/ContactInbox.cs) |
| `src/HiringPs.Web/Models/ContactModels.cs` | `ContactInput` | [ContactInput.cs](../src/HiringPs.Web/Models/Contact/Inputs/ContactInput.cs) |
| `src/HiringPs.Web/Models/ContactModels.cs` | `ContactReplyLinks` | [ContactReplyLinks.cs](../src/HiringPs.Web/Models/Contact/ViewModels/ContactReplyLinks.cs) |
| `src/HiringPs.Web/Models/ErrorViewModel.cs` | `ErrorViewModel` | [ErrorViewModel.cs](../src/HiringPs.Web/Models/Shared/ViewModels/ErrorViewModel.cs) |
| `src/HiringPs.Web/Models/JawwalPaySetup.cs` | `JawwalPaySetup` | [JawwalPaySetup.cs](../src/HiringPs.Web/Models/Payments/ViewModels/JawwalPaySetup.cs) |
| `src/HiringPs.Web/Models/JawwalPaySetup.cs` | `PaymentEndpoint` | [PaymentEndpoint.cs](../src/HiringPs.Web/Models/Payments/ViewModels/PaymentEndpoint.cs) |
| `src/HiringPs.Web/Models/ListingModel.cs` | `ListingModel` | [ListingModel.cs](../src/HiringPs.Web/Models/Opportunities/ViewModels/ListingModel.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `MemberDashboard` | [MemberDashboard.cs](../src/HiringPs.Web/Models/Membership/ViewModels/MemberDashboard.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `PendingVerification` | [PendingVerification.cs](../src/HiringPs.Web/Models/Membership/ViewModels/PendingVerification.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `PlanCatalog` | [PlanCatalog.cs](../src/HiringPs.Web/Models/Membership/ViewModels/PlanCatalog.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `PlanInput` | [PlanInput.cs](../src/HiringPs.Web/Models/Membership/Inputs/PlanInput.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `PreferenceInput` | [PreferenceInput.cs](../src/HiringPs.Web/Models/Membership/Inputs/PreferenceInput.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `PurchaseInput` | [PurchaseInput.cs](../src/HiringPs.Web/Models/Membership/Inputs/PurchaseInput.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `RegisterInput` | [RegisterInput.cs](../src/HiringPs.Web/Models/Membership/Inputs/RegisterInput.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `ResetInput` | [ResetInput.cs](../src/HiringPs.Web/Models/Membership/Inputs/ResetInput.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `ResetPasswordInput` | [ResetPasswordInput.cs](../src/HiringPs.Web/Models/Membership/Inputs/ResetPasswordInput.cs) |
| `src/HiringPs.Web/Models/MemberInputs.cs` | `VerificationInput` | [VerificationInput.cs](../src/HiringPs.Web/Models/Membership/Inputs/VerificationInput.cs) |
| `src/HiringPs.Web/Models/PaymentActions.cs` | `PaymentActions` | [PaymentActions.cs](../src/HiringPs.Web/Models/Payments/ViewModels/PaymentActions.cs) |
| `src/HiringPs.Web/Models/PaymentDisplay.cs` | `PaymentDisplay` | [PaymentDisplay.cs](../src/HiringPs.Web/Models/Payments/ViewModels/PaymentDisplay.cs) |
| `src/HiringPs.Web/Models/PreferenceChoices.cs` | `PreferenceChoices` | [PreferenceChoices.cs](../src/HiringPs.Web/Models/Membership/ViewModels/PreferenceChoices.cs) |
| `src/HiringPs.Web/Models/SaveButton.cs` | `SaveButton` | [SaveButton.cs](../src/HiringPs.Web/Models/Opportunities/ViewModels/SaveButton.cs) |
| `src/HiringPs.Web/Models/SiteFacts.cs` | `SiteFacts` | [SiteFacts.cs](../src/HiringPs.Web/Models/Content/ViewModels/SiteFacts.cs) |
| `src/HiringPs.Web/Models/SiteFacts.cs` | `SiteQuestion` | [SiteQuestion.cs](../src/HiringPs.Web/Models/Content/ViewModels/SiteQuestion.cs) |
| `src/HiringPs.Web/Models/SitePageModels.cs` | `SitePageDefaults` | [SitePageDefaults.cs](../src/HiringPs.Web/Models/Content/ViewModels/SitePageDefaults.cs) |
| `src/HiringPs.Web/Models/SitePageModels.cs` | `SitePageInput` | [SitePageInput.cs](../src/HiringPs.Web/Models/Content/Inputs/SitePageInput.cs) |
| `src/HiringPs.Web/Models/SitePageModels.cs` | `SitePageViewModel` | [SitePageViewModel.cs](../src/HiringPs.Web/Models/Content/ViewModels/SitePageViewModel.cs) |
| `src/HiringPs.Web/Models/SiteQuestionsModels.cs` | `SiteQuestionInput` | [SiteQuestionInput.cs](../src/HiringPs.Web/Models/Content/Inputs/SiteQuestionInput.cs) |
| `src/HiringPs.Web/Models/SiteQuestionsModels.cs` | `SiteQuestionsContent` | [SiteQuestionsContent.cs](../src/HiringPs.Web/Models/Content/ViewModels/SiteQuestionsContent.cs) |
| `src/HiringPs.Web/Models/SiteQuestionsModels.cs` | `SiteQuestionsInput` | [SiteQuestionsInput.cs](../src/HiringPs.Web/Models/Content/Inputs/SiteQuestionsInput.cs) |
| `src/HiringPs.Web/Models/SiteSeo.cs` | `SeoMetadata` | [SeoMetadata.cs](../src/HiringPs.Web/Models/Content/ViewModels/SeoMetadata.cs) |
| `src/HiringPs.Web/Models/SiteSeo.cs` | `SiteSeo` | [SiteSeo.cs](../src/HiringPs.Web/Models/Content/ViewModels/SiteSeo.cs) |
| `src/HiringPs.Web/Models/SourceClock.cs` | `SourceClock` | [SourceClock.cs](../src/HiringPs.Web/Models/Collection/ViewModels/SourceClock.cs) |
| `src/HiringPs.Web/Models/ToolDashboard.cs` | `ToolDashboard` | [ToolDashboard.cs](../src/HiringPs.Web/Models/Monitoring/ViewModels/ToolDashboard.cs) |
| `src/HiringPs.Web/Models/UiMessages.cs` | `UiMessages` | [UiMessages.cs](../src/HiringPs.Web/Models/Shared/ViewModels/UiMessages.cs) |
| `src/HiringPs.Web/Models/WalletCheckoutPage.cs` | `WalletCheckoutPage` | [WalletCheckoutPage.cs](../src/HiringPs.Web/Models/Payments/ViewModels/WalletCheckoutPage.cs) |
| `src/HiringPs.Web/Models/WalletCheckoutPage.cs` | `WalletReceiptSummary` | [WalletReceiptSummary.cs](../src/HiringPs.Core/Features/Payments/JawwalPay/Dtos/WalletReceiptSummary.cs) |
| `src/HiringPs.Web/Models/WorkflowModels.cs` | `AiUsageDashboard` | [AiUsageDashboard.cs](../src/HiringPs.Web/Models/Ai/ViewModels/AiUsageDashboard.cs) |
| `src/HiringPs.Web/Models/WorkflowModels.cs` | `AiUsageTotals` | [AiUsageTotals.cs](../src/HiringPs.Web/Models/Ai/ViewModels/AiUsageTotals.cs) |
| `src/HiringPs.Web/Models/WorkflowModels.cs` | `PromptEditor` | [PromptEditor.cs](../src/HiringPs.Web/Models/Ai/Inputs/PromptEditor.cs) |
| `src/HiringPs.Web/Models/WorkflowModels.cs` | `RetentionEditor` | [RetentionEditor.cs](../src/HiringPs.Web/Models/Ai/Inputs/RetentionEditor.cs) |

## فصل المسؤوليات بعد نقل الملفات

| الواجهة السابقة | التنفيذ المفصول |
| --- | --- |
| OpportunityService: قواعد التحقق | Features/Opportunities/Rules/OpportunityValidationRules.cs |
| OpportunityService: تطبيق الاستخراج | Features/Opportunities/Services/OpportunityExtractionService.cs |
| CollectionPipeline: معالجة عنصر | Features/Collection/Services/SourceItemProcessingService.cs |
| CollectionPipeline: حفظ صفحة اكتشاف | Persistence/Services/DiscoveryBatchStore.cs |
| CollectionPipeline: وصف الأخطاء | Features/Collection/Rules/CollectionErrorRules.cs |
| SocialController: حجز وإرسال محاولة | Features/SocialPublishing/Shared/Services/SocialPublicationService.cs |

المسارات في الجدول نسبية إلى `src/HiringPs.Core`. بقيت واجهات OpportunityService العامة متوافقة. عقود أوامر النشر الجديدة موجودة في Dtos وEnums المنفصلة ضمن SocialPublishing/Shared.

| المسار | المسؤولية المفصولة |
| --- | --- |
| Features/SocialPublishing/Shared/Services/SocialComposeReadService.cs | قراءة شاشة تجهيز المنشور عبر DTOs |
| Features/Membership/Services/SubscriptionReadService.cs | قراءة قائمة الاشتراكات الإدارية وأسماء النسخ |
| Features/Membership/Services/SubscriptionManagementService.cs | إيقاف الاشتراك مع قفل العضو والمعاملة |

نموذج أمر النشر `SocialPublishInput` منفصل عن `SocialCompose` تحت `src/HiringPs.Web/Models/SocialPublishing/Inputs`؛ أسماء حقول النموذج وروابط الإجراءات بقيت كما هي.

| المسار تحت Core/Features/Membership | المسؤولية |
| --- | --- |
| Services/MemberPlanReadService.cs | كتالوج الباقات وفترات العضو وشروط النسخة الحالية عبر DTOs |
| Services/MembershipDiscountService.cs | أهلية الخصم المشتركة بين المعاينة والحجز |
| Services/MembershipCheckoutService.cs | حجز طلب الاشتراك مع قفل العضو وتثبيت الشروط والمبلغ |
| Rules/SubscriptionPeriodDisplayRules.cs | دمج الفترات المتصلة للعرض دون تغيير السجلات |

إسقاط شروط النسخة موجود تحت `Persistence/Queries/MembershipPlanProjection.cs`. بيانات العرض في `PlanCatalog` و`PurchaseInput.Plan` أصبحت DTOs؛ مسارات HTTP وأسماء حقول النموذج بقيت ثابتة.

## استكمال الإدارة والأسعار والتسوية

| المجال | الخدمات والعقود |
| --- | --- |
| إدارة الباقات | AdminPlanReadService، PlanManagementService، OpportunityAccessService؛ DTOs تحت Membership/Dtos |
| الخصومات | DiscountManagementService وDiscountInput؛ لا يربط نموذج HTTP بكيان DiscountCode |
| الأعضاء | AdminMemberReadService وMemberRecoveryService؛ حقول العرض مختصرة دون بيانات الربط الداخلية |
| المصادر | SourceReadService، SourceManagementService، SourceCollectionRequestService؛ SourceInput وSourceEditDto منفصلان |
| الفرص | AdminOpportunityReadService، OpportunityManagementService؛ OpportunityInput وOpportunityEditDto منفصلان |
| قوالب النشر | SocialTemplateManagementService وSocialTemplateSummaryDto مع قفل SQL الأصلي |
| التكاملات | SocialIntegrationSummaryDto دون الرمز المشفر |
| التسوية | PaymentRecoveryService واختيار مستقل لكل طلب في MembershipWorker |
| الأسعار المباشرة | Web/Services/Membership/PlanCatalogNotifications، PlanUpdatesController، plan-catalog.js، plan-preview.js |

راجع PAYMENT-RECOVERY.md وLIVE-PLAN-PRICES.md لحدود المزود والتشغيل. كل التغييرات محلية وغير مرفوعة.


## أنواع إضافات المنصة المدمجة

| الملف في تحديث المستخدم | النوع | المسار المنظم |
| --- | --- | --- |
| `Domain.cs` | `Source` | [Source.cs](../src/HiringPs.Core/Persistence/Entities/Collection/Source.cs) |
| `Domain.cs` | `SourceItem` | [SourceItem.cs](../src/HiringPs.Core/Persistence/Entities/Collection/SourceItem.cs) |
| `Domain.cs` | `Opportunity` | [Opportunity.cs](../src/HiringPs.Core/Persistence/Entities/Opportunities/Opportunity.cs) |
| `Domain.cs` | `WorkerHeartbeat` | [WorkerHeartbeat.cs](../src/HiringPs.Core/Persistence/Entities/Collection/WorkerHeartbeat.cs) |
| `Domain.cs` | `PipelineState` | [PipelineState.cs](../src/HiringPs.Core/Persistence/Entities/Collection/PipelineState.cs) |
| `Domain.cs` | `OrganizationAlias` | [OrganizationAlias.cs](../src/HiringPs.Core/Persistence/Entities/Opportunities/OrganizationAlias.cs) |
| `Domain.cs` | `DuplicateCandidateStatus` | [DuplicateCandidateStatus.cs](../src/HiringPs.Core/Features/Opportunities/Enums/DuplicateCandidateStatus.cs) |
| `Domain.cs` | `DuplicateCandidate` | [DuplicateCandidate.cs](../src/HiringPs.Core/Persistence/Entities/Opportunities/DuplicateCandidate.cs) |
| `Domain.cs` | `TextRules` | [TextRules.cs](../src/HiringPs.Core/Features/Shared/Rules/TextRules.cs) |
| `Collection.cs` | `UrlPolicy` | [BrowserCollector.Collection.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserCollector.Collection.cs) |
| `Collection.cs` | `ContentFingerprint` | [BrowserCollector.Collection.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserCollector.Collection.cs) |
| `Collection.cs` | `BrowserCollector` | [BrowserCollector.Collection.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserCollector.Collection.cs) |
| `Attachments.cs` | `ContentNeedsReviewException` | [AttachmentReader.cs](../src/HiringPs.Core/Features/Pdf/Services/AttachmentReader.cs) |
| `Attachments.cs` | `ContentRetryableException` | [AttachmentReader.cs](../src/HiringPs.Core/Features/Pdf/Services/AttachmentReader.cs) |
| `Attachments.cs` | `AttachmentReader` | [AttachmentReader.cs](../src/HiringPs.Core/Features/Pdf/Services/AttachmentReader.cs) |
| `DateEvidenceRules.cs` | `DateEvidenceRules` | [DateEvidenceRules.cs](../src/HiringPs.Core/Features/Ai/Rules/DateEvidenceRules.cs) |
| `OpportunityRss.cs` | `OpportunityRss` | [OpportunityRss.cs](../src/HiringPs.Core/Features/Opportunities/Services/OpportunityRss.cs) |
| `WorkflowServices.cs` | `PromptWorkflow` | [PromptWorkflow.cs](../src/HiringPs.Core/Features/Ai/Services/PromptWorkflow.cs) |
| `AiGovernor.cs` | `AiGovernor` | [AiGovernor.cs](../src/HiringPs.Core/Features/Ai/Services/AiGovernor.cs) |
| `BrowserHost.cs` | `BrowserHost` | [BrowserHost.cs](../src/HiringPs.Core/Features/Collection/Services/BrowserHost.cs) |
| `DuplicateMatcher.cs` | `DuplicateMatcher` | [DuplicateMatcher.cs](../src/HiringPs.Core/Features/Opportunities/Services/DuplicateMatcher.cs) |
| `ExtractionValidator.cs` | `ExtractionValidator` | [ExtractionValidator.cs](../src/HiringPs.Core/Features/Ai/Rules/ExtractionValidator.cs) |
| `GeminiClient.cs` | `ExtractionResult` | [ExtractionResult.cs](../src/HiringPs.Core/Features/Ai/Dtos/ExtractionResult.cs) |
| `GeminiClient.cs` | `ExtractedOpportunity` | [ExtractedOpportunity.cs](../src/HiringPs.Core/Features/Ai/Dtos/ExtractedOpportunity.cs) |
| `GeminiClient.cs` | `AiUnavailableException` | [AiUnavailableException.cs](../src/HiringPs.Core/Features/Ai/Exceptions/AiUnavailableException.cs) |
| `GeminiClient.cs` | `AiQuotaException` | [AiQuotaException.cs](../src/HiringPs.Core/Features/Ai/Exceptions/AiQuotaException.cs) |
| `GeminiClient.cs` | `AiTransientException` | [AiTransientException.cs](../src/HiringPs.Core/Features/Ai/Exceptions/AiTransientException.cs) |
| `GeminiClient.cs` | `GeminiClient` | [GeminiClient.cs](../src/HiringPs.Core/Features/Ai/Services/GeminiClient.cs) |
| `GeminiClient.cs` | `AiExtractor` | [AiExtractor.cs](../src/HiringPs.Core/Features/Ai/Services/AiExtractor.cs) |
| `IndexNow.cs` | `PublicUrls` | [PublicUrls.cs](../src/HiringPs.Core/Features/Indexing/Rules/PublicUrls.cs) |
| `IndexNow.cs` | `IndexNowEntry` | [IndexNowEntry.cs](../src/HiringPs.Core/Persistence/Entities/Indexing/IndexNowEntry.cs) |
| `IndexNow.cs` | `IndexNowSettings` | [IndexNowSettings.cs](../src/HiringPs.Core/Features/Indexing/Rules/IndexNowSettings.cs) |
| `IndexNow.cs` | `IndexNowQueue` | [IndexNowQueue.cs](../src/HiringPs.Core/Features/Indexing/Services/IndexNowQueue.cs) |
| `IndexNow.cs` | `IndexNowSubmitter` | [IndexNowSubmitter.cs](../src/HiringPs.Core/Features/Indexing/Services/IndexNowSubmitter.cs) |
| `SourceHealth.cs` | `SourceHealth` | [SourceHealth.cs](../src/HiringPs.Core/Features/Collection/Rules/SourceHealth.cs) |
| `TelegramChannel.cs` | `TelegramChannelSetting` | [TelegramChannelSetting.cs](../src/HiringPs.Core/Persistence/Entities/Telegram/TelegramChannelSetting.cs) |
| `TelegramChannel.cs` | `TelegramChannelTargets` | [TelegramChannelTargets.cs](../src/HiringPs.Core/Features/Telegram/Rules/TelegramChannelTargets.cs) |
| `TelegramChannel.cs` | `TelegramChannelResult` | [TelegramChannelResult.cs](../src/HiringPs.Core/Features/Telegram/Dtos/TelegramChannelResult.cs) |
| `TelegramChannel.cs` | `TelegramChannelOutcome` | [TelegramChannelOutcome.cs](../src/HiringPs.Core/Features/Telegram/Enums/TelegramChannelOutcome.cs) |
| `TelegramChannel.cs` | `TelegramChannelClient` | [TelegramChannelClient.cs](../src/HiringPs.Core/Features/Telegram/Services/TelegramChannelClient.cs) |
| `TelegramChannel.cs` | `TelegramChannelPublisher` | [TelegramChannelPublisher.cs](../src/HiringPs.Core/Features/Telegram/Services/TelegramChannelPublisher.cs) |
| `src/HiringPs.Web/Models/CityCatalog.cs` | `City` | [City.cs](../src/HiringPs.Web/Models/Opportunities/Dtos/City.cs) |
| `src/HiringPs.Web/Models/CityCatalog.cs` | `CityCatalog` | [CityCatalog.cs](../src/HiringPs.Web/Services/PublicCatalog/CityCatalog.cs) |
| `src/HiringPs.Web/Models/FilterLabels.cs` | `FilterOption` | [FilterOption.cs](../src/HiringPs.Web/Models/Opportunities/Dtos/FilterOption.cs) |
| `src/HiringPs.Web/Models/FilterLabels.cs` | `FilterLabels` | [FilterLabels.cs](../src/HiringPs.Web/Services/PublicCatalog/FilterLabels.cs) |
| `src/HiringPs.Web/Models/PublicCatalog.cs` | `FilterValues` | [FilterValues.cs](../src/HiringPs.Web/Models/Opportunities/Dtos/FilterValues.cs) |
| `src/HiringPs.Web/Models/PublicCatalog.cs` | `CityFacet` | [CityFacet.cs](../src/HiringPs.Web/Models/Opportunities/Dtos/CityFacet.cs) |
| `src/HiringPs.Web/Models/PublicCatalog.cs` | `OrganizationFacet` | [OrganizationFacet.cs](../src/HiringPs.Web/Models/Opportunities/Dtos/OrganizationFacet.cs) |
| `src/HiringPs.Web/Models/PublicCatalog.cs` | `BrowseFacets` | [BrowseFacets.cs](../src/HiringPs.Web/Models/Opportunities/Dtos/BrowseFacets.cs) |
| `src/HiringPs.Web/Models/PublicCatalog.cs` | `PublicCatalog` | [PublicCatalog.cs](../src/HiringPs.Web/Services/PublicCatalog/PublicCatalog.cs) |
| `src/HiringPs.Web/Models/PublicListing.cs` | `SiteCache` | [SiteCache.cs](../src/HiringPs.Web/Services/PublicCatalog/SiteCache.cs) |
| `src/HiringPs.Web/Models/PublicListing.cs` | `PublicListing` | [PublicListing.cs](../src/HiringPs.Web/Services/PublicCatalog/PublicListing.cs) |
| `src/HiringPs.Web/Models/PublicListing.cs` | `KindSlugs` | [KindSlugs.cs](../src/HiringPs.Web/Services/PublicCatalog/KindSlugs.cs) |
| `src/HiringPs.Web/Models/PublicListing.cs` | `UrlSlug` | [UrlSlug.cs](../src/HiringPs.Web/Services/PublicCatalog/UrlSlug.cs) |

توجد خدمات قراءة وإدارة التوزيع ضمن `Features/Telegram`، وإدارة التكرار والجهات ضمن `Features/Opportunities`. نماذج القراءة منفصلة عن كيانات EF. خدمات الفلاتر والروابط العامة ضمن `HiringPs.Web/Services/PublicCatalog`.


## دفعة خدمات الإدارة والقراءة

| المسار السابق أو المسؤولية | المسار الحالي |
| --- | --- |
| `MessagesController` — قراءة الرسائل ومعالجتها | `Features/Contact/{Services,Dtos}` |
| `PagesController` وحفظ الأسئلة | `Features/Content/{Services,Dtos,Rules}` |
| `ReportsController` وقراءة سجل الدمج | `Features/Opportunities/Services/{ReportManagementService,MergeAdministrationService}.cs` |
| `NotificationsController` — القراءة وجدولة إعادة المحاولة | `Features/Telegram/Services/NotificationAdministrationService.cs` |
| `AdminController` — المؤشرات والجولات | `Features/Monitoring/Services/AdminDashboardReadService.cs` |
| `ItemsController` — القراءة وإعادة المعالجة | `Features/Collection/Services/SourceItemAdministrationService.cs` |
| `DiagnosticsController` — الملفات والبيانات التشخيصية | `Features/Monitoring/Services/CollectorDiagnosticReadService.cs` |
| `AiUsageController` — الاستعلامات والتجميع | `Features/Ai/Services/AiUsageReadService.cs` |
| `Models/Ai/ViewModels/AiUsageTotals.cs` | `Features/Ai/Dtos/AiUsageTotalsDto.cs` |
| `AiPromptController` — الإصدارات والقواعد | `Features/Ai/Services/PromptAdministrationService.cs` و`Features/Ai/Rules/PromptInputRules.cs` |
| `RetentionController` — حفظ السياسة | `Features/Retention/Services/RetentionSettingsService.cs` |
| `OpportunityExtractionService` — تجهيز المرشح | `Features/Opportunities/Rules/ExtractionCandidateRules.cs` |
| `_DateExtraction.cshtml` — تحليل JSON ودليل PDF | `HiringPs.Web/Services/Collection/SourceItemEvidencePresenter.cs` |

المسارات غير المسبوقة باسم مشروع في الجدول تبدأ من `src/HiringPs.Core`. يحتوي كل DTO جديد على ملف مستقل ضمن مجال خدمته. راجع [متابعة الجودة](QUALITY-PROGRESS.md) للقيود والميزة المؤجلة.


## قراءات الزائر ومساحة العضو

| المسؤولية | المسار |
| --- | --- |
| كارد وتفاصيل الفرصة وروابط المصدر | `Core/Features/Opportunities/Dtos/{OpportunityCardDto,OpportunityDetailsDto,OpportunitySourceSummaryDto,VerifiedOpportunityDateDto}.cs` |
| إسقاط SQL لبطاقات الفرص | `Core/Persistence/Queries/PublicOpportunityProjection.cs` |
| نطاق الحالي والأرشيف والفهرسة العامة | `Core/Features/Opportunities/Rules/PublicOpportunityScope.cs` |
| الرئيسية والتصفح وRSS | `Web/Services/PublicCatalog/PublicListingReadService.cs` |
| التفاصيل والأدلة والبلاغ | `Core/Features/Opportunities/Services/{OpportunityDetailsReadService,OpportunityReportService}.cs` |
| بيانات خريطة الموقع | `Core/Features/Opportunities/Services/PublicSitemapReadService.cs` |
| محفوظات العضو وحالة الحساب | `Core/Features/Membership/Services/{SavedOpportunityService,MemberAccountReadService}.cs` |
| تفضيلات العضو وحفظ الإعدادات | `Core/Features/Membership/Services/{MemberPreferenceService,MemberNotificationSettingsService}.cs` |
| محرر التفضيلات | `Web/Services/Membership/MemberPreferencePageService.cs` |
| قراءة زر ربط جوجل وقالب عرضه | `Web/Services/Authentication/GoogleLinkReadService.cs` و`Web/ViewComponents/GoogleLinkViewComponent.cs` |
| بصمة المحتوى المستعادة من Collection.cs | `Core/Features/Collection/Rules/ContentFingerprint.cs` |
| استثناء المصدر المؤقت المستعاد من Attachments.cs | `Core/Features/Collection/Exceptions/ContentRetryableException.cs` |

`Core` و`Web` في الجدول اختصار لـ`src/HiringPs.Core` و`src/HiringPs.Web`. خدمات العضو مسجلة عبر `AddMemberWorkspaceFeatures`، وقراءات الزائر عبر `AddPublicOpportunityFeatures`.
