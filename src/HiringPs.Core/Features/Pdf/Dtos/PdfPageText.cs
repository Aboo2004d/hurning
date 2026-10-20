using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>DTO لنص صفحة PDF وطريقة قراءتها وثقة OCR إن توفرت.</summary>
public record PdfPageText(int Page, string Method, string Text, double? Confidence = null);
