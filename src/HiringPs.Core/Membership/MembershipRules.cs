using System.Globalization;
using System.Text.Json;

namespace HiringPs.Core;

public static class MembershipRules
{
    public const int AllKinds = 15;

    public static int Bit(OpportunityKind kind) => Enum.IsDefined(kind) ? 1 << (int)kind : 0;

    public static bool Includes(int mask, OpportunityKind kind) => (mask & Bit(kind)) != 0;

    public static bool Paid(PaymentState state) =>
        state is PaymentState.Paid or PaymentState.ManuallyApproved;

    public static decimal Amount(decimal price, int percent) =>
        Math.Round(price * (100 - percent) / 100, 2, MidpointRounding.AwayFromZero);

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

    public static DateTimeOffset Starts(
        DateTimeOffset now,
        IEnumerable<MemberSubscription> subscriptions
    ) =>
        subscriptions
            .Where(x => !x.Cancelled && x.EndsAt > now)
            .Select(x => x.EndsAt)
            .Append(now)
            .Max();

    public static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture) + " ILS";

    // Keep financial rows separate; combine consecutive renewals only for display.
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
