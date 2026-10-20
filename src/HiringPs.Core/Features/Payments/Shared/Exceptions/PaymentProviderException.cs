using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>خطأ بوابة دفع برمز تشخيص آمن وحالة HTTP دون كشف رد المزود الكامل.</summary>
public sealed class PaymentProviderException : Exception
{
    public PaymentProviderException(
        string reasonCode = "ProviderUnavailable",
        int? httpStatus = null
    )
        : base("GatewayUnavailable")
    {
        ReasonCode = reasonCode;
        HttpStatus = httpStatus;
    }

    public string ReasonCode { get; }
    public int? HttpStatus { get; }
    public WalletReceiptValidationFacts? ReceiptFacts { get; init; }
}
