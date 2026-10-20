using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>رفض صريح لطلب المحفظة يميز الرفض المؤكد عن النتيجة غير المحسومة.</summary>
public sealed class WalletRequestRejectedException : Exception
{
    public WalletRequestRejectedException()
        : base("WalletRequestRejected") { }
}
