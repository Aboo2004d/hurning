using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مرحلة تشخيص دفع تحتوي رموزًا وحالة HTTP فقط دون نص استثناء أو أسرار.</summary>
public sealed record PaymentTraceStep(string Stage, string Code, int? HttpStatus);
