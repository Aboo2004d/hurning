// تنزيل PDF ضمن الحدود المسموحة ثم قراءة الصفحات وحفظ النصوص بواسطة مسار النص وOCR.
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HiringPs.Core;

/// <summary>DTO يجمع النص الخام وروابط مرفقاته قبل استخراج الفرص.</summary>
public record ContentBundle(string Text, string[] Attachments);
