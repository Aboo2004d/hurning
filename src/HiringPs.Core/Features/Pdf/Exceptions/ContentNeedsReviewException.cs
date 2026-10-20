// تنزيل PDF ضمن الحدود المسموحة ثم قراءة الصفحات وحفظ النصوص بواسطة مسار النص وOCR.
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HiringPs.Core;

/// <summary>إشارة إلى أن المحتوى لا يمكن اعتماده آليًا ويحتاج مراجعة بشرية.</summary>
public class ContentNeedsReviewException(string message) : Exception(message);
