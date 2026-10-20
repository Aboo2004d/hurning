using HiringPs.Core;

namespace HiringPs.Web.Models;

// Use the project's Palestine zone (including DST), never a fixed UTC offset.
public static class PaymentDisplay
{
    public static string Time(DateTimeOffset instant, bool seconds = false) =>
        SourceSchedule.Display(instant, seconds);

    public static string Time(DateTimeOffset? instant, bool seconds = false) =>
        instant is { } value ? Time(value, seconds) : "—";
}
