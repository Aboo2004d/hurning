using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يربط فشل الجمع بحقائق تشخيص محدودة وبالخطأ الأصلي داخل العملية.</summary>
public class CollectorDiagnosticException(CollectorDiagnostic diagnostic, Exception original)
    : Exception(original.Message, original)
{
    public CollectorDiagnostic Diagnostic { get; } = diagnostic;
}
