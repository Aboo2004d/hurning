using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>DTO لمرجع ورابط الدفع المستضاف الذي تحقق منه المحول.</summary>
/// <param name="Reference">المرجع الذي أنشأه أو قبله المزود لهذه العملية.</param>
/// <param name="Url">عنوان صفحة الدفع الذي تحقق المحول من نطاقه وصيغته.</param>
public sealed record GatewayCheckout(string Reference, Uri Url);
