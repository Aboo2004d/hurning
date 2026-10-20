using System.Globalization;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>قواعد حساب الصلاحيات والمبلغ والمدة والهاتف المشتركة بين مسارات العضوية.</summary>
public static class MembershipRules
{
    public const int AllKinds = 15;

    /// <summary>يحسب بت نوع الفرصة المستخدم في أقنعة صلاحيات الاشتراك.</summary>
    public static int Bit(OpportunityKind kind) => Enum.IsDefined(kind) ? 1 << (int)kind : 0;

    /// <summary>يتحقق من احتواء قناع الأنواع على نوع فرصة محدد.</summary>
    public static bool Includes(int mask, OpportunityKind kind) => (mask & Bit(kind)) != 0;

    /// <summary>يميز حالات الدفع التي تمنح الاستحقاق وفق سياسة العضوية.</summary>
    public static bool Paid(PaymentState state) =>
        state is PaymentState.Paid or PaymentState.ManuallyApproved;

    /// <summary>يحسب المبلغ بعد الخصم وفق قواعد التقريب المالي الحالية.</summary>
    public static decimal Amount(decimal price, int percent) =>
        Math.Round(price * (100 - percent) / 100, 2, MidpointRounding.AwayFromZero);

    /// <summary>يطبع رقم الهاتف ويتحقق من صيغته المدعومة.</summary>
    public static string Mobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("MobileInternational");
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00"))
            digits = digits[2..];
        if (digits.Length is < 8 or > 15 || digits.StartsWith('0'))
            throw new InvalidOperationException("MobileInternational");
        return "+" + digits;
    }

    /// <summary>يطابق القيمة مع القيم أو الشروط المعتمدة في هذا المجال.</summary>
    public static bool Matches(MemberPreference preference, Opportunity opportunity)
    {
        var categories = JsonSerializer.Deserialize<string[]>(preference.CategoriesJson) ?? [];
        var locations = JsonSerializer.Deserialize<string[]>(preference.LocationsJson) ?? [];
        return Includes(preference.Kinds, opportunity.Kind)
            && (
                categories.Length == 0
                || categories.Contains(opportunity.Category, StringComparer.OrdinalIgnoreCase)
            )
            && (
                locations.Length == 0
                || locations.Any(location =>
                    opportunity.LocationSearchText.Contains(
                        LocationSearch.Normalize(location),
                        StringComparison.Ordinal
                    )
                )
            );
    }

    /// <summary>يحسب بداية الاستحقاق وفق وضع الاشتراك الحالي.</summary>
    public static DateTimeOffset Starts(
        DateTimeOffset now,
        IEnumerable<MemberSubscription> subscriptions
    ) =>
        subscriptions
            .Where(x => !x.Cancelled && x.EndsAt > now)
            .Select(x => x.EndsAt)
            .Append(now)
            .Max();

    /// <summary>ينسق المبلغ المالي أو يقربه وفق قاعدة المجال الحالية.</summary>
    public static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture) + " ILS";

    // Keep financial rows separate; combine consecutive renewals only for display.
    /// <summary>يحول فترات الاشتراك إلى وصف واضح للعرض.</summary>
    public static List<MemberSubscription> DisplayPeriods(
        IEnumerable<MemberSubscription> subscriptions
    )
    {
        var result = new List<MemberSubscription>();
        foreach (var sub in subscriptions.Where(x => !x.Cancelled).OrderBy(x => x.StartsAt))
        {
            if (
                result.LastOrDefault() is { } previous
                && previous.PlanVersionId == sub.PlanVersionId
                && previous.EndsAt == sub.StartsAt
            )
                previous.EndsAt = sub.EndsAt;
            else
                result.Add(
                    new MemberSubscription
                    {
                        Id = sub.Id,
                        UserId = sub.UserId,
                        PlanVersionId = sub.PlanVersionId,
                        PlanVersion = sub.PlanVersion,
                        StartsAt = sub.StartsAt,
                        EndsAt = sub.EndsAt,
                        OrderId = sub.OrderId,
                    }
                );
        }
        return result;
    }
}
