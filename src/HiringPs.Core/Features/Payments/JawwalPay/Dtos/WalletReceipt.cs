using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>DTO للإيصال المالي الذي تم تحليله؛ تفصل مبالغه الإجمالية والرسوم وصافي القيد.</summary>
public sealed record WalletReceipt(
    long TransactionId,
    string? Reference,
    decimal Amount,
    decimal JawwalFee,
    decimal PlatformFee,
    decimal NetCredited
)
{
    public bool IsSandboxSimulation { get; init; }
}
