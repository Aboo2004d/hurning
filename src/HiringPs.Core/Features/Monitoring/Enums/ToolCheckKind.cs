namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـToolCheckKind؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum ToolCheckKind
{
    Standard = 0,
    AiLive = 1,
    GoogleLogin = 2,
    Facebook = 3,
    LinkedIn = 4,
    All = 5,
    Collector = 6,
    AiConfiguration = 7,
    Chromium = 8,
    PdfText = 9,
    PdfRender = 10,
    PdfOcr = 11,
    PdfLanguages = 12,
    PdfWorkspace = 13,
    PdfPipeline = 14,
    LahzaPayment = 15,
    JawwalPayPayment = 16,
    LinkedInPersonal = 17,
}
