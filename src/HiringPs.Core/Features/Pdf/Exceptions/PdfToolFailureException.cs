using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>فشل أداة أصلية قابل للعرض الإداري وفق حدود تشخيص PDF الآمنة.</summary>
public sealed class PdfToolFailureException(string message) : ContentNeedsReviewException(message);
