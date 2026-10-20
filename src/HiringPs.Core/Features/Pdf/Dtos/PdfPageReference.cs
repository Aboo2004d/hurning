using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>DTO لمرجع صفحة محفوظة يربط الدليل ببصمة الملف ورقم الصفحة.</summary>
public record PdfPageReference(string Hash, int Page);
