using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>DTO لملخص رصيد المحفظة وإجمالي الرسوم والمعاملات والسحوبات.</summary>
public sealed record WalletBalance(
    decimal Balance,
    decimal Available,
    decimal GrossTotal,
    decimal JawwalFees,
    decimal PlatformFees,
    decimal CreditedTotal,
    decimal WithdrawnTotal,
    decimal PendingWithdrawals,
    long TransactionsCount
);
