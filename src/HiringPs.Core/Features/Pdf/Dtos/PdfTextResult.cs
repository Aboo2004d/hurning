using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>DTO لنتيجة قراءة PDF مع بصمة الملف وأدلة صفحاته.</summary>
public record PdfTextResult(string Hash, string Text, IReadOnlyList<PdfPageText> Pages);
