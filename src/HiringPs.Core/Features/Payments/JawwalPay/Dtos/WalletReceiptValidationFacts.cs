using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

// Whitelisted diagnostic facts for administrators, never the original provider body.
/// <summary>حقائق مالية مسموحة للتشخيص الإداري بدل حفظ حمولة المزود الأصلية.</summary>
public sealed record WalletReceiptValidationFacts(
    long TransactionId,
    decimal ExpectedAmountIls,
    decimal ReceivedAmountIls,
    decimal JawwalFeeIls,
    decimal PlatformFeeIls,
    decimal NetAmountIls
);
