using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>DTO لحقيقة شبكة محدودة: العنوان المصنف ونوع الحدث والسبب وحالة HTTP.</summary>
public record NetworkDiagnostic(string Url, string Type, string Reason, int? Status);
