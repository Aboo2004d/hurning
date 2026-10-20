using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>DTO لطلب تحقق أنشئ حديثًا؛ يحمل السر اللازم للرابط ولا يخزن كما هو في القاعدة.</summary>
public sealed record VerificationRequest(Guid Id, string Token, ChallengePurpose Purpose);
