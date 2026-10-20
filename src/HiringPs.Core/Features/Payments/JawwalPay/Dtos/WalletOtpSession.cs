using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>DTO قصير العمر لجلسة المحفظة؛ لا يطبع مفتاح الجلسة في ToString.</summary>
public sealed class WalletOtpSession(string sessionKey, decimal amount, int expiresIn)
{
    /// <summary>سر جلسة التأكيد؛ يشفّر قبل التخزين ولا يسجل أو يعرض ضمن وصف الكائن.</summary>
    public string SessionKey { get; } = sessionKey;
    /// <summary>المبلغ الذي وافق عليه المزود عند بدء الجلسة.</summary>
    public decimal Amount { get; } = amount;
    /// <summary>مدة صلاحية الجلسة بالثواني كما أعادها المزود.</summary>
    public int ExpiresIn { get; } = expiresIn;

    public override string ToString() => "Wallet OTP session";
}
