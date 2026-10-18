using System.Globalization;
using HiringPs.Web.Models;

public static class PaymentDisplayChecks
{
    public static void Run()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            Check(
                ReferenceEquals(
                    HiringPs.Core.SourceSchedule.Zone,
                    HiringPs.Core.TextRules.PalestineTimeZone
                ),
                "Payments, collection and PalestineToday use the same resolved project time zone"
            );
            Check(
                PaymentDisplay.Time(
                    DateTimeOffset.Parse("2026-01-18T14:34:30Z", CultureInfo.InvariantCulture)
                ) == "2026-01-18 16:34",
                "Winter payment time uses Palestine's winter offset"
            );
            Check(
                PaymentDisplay.Time(
                    DateTimeOffset.Parse("2026-07-18T14:34:30Z", CultureInfo.InvariantCulture)
                ) == "2026-07-18 17:34",
                "Summer payment time uses Palestine DST"
            );
            Check(
                PaymentDisplay.Time(
                    DateTimeOffset.Parse("2026-10-18T14:34:30Z", CultureInfo.InvariantCulture),
                    true
                ) == "2026-10-18 17:34:30",
                "Trace seconds use Gregorian Palestine time independent of Arabic calendar"
            );
            Check(
                PaymentDisplay.Time(
                    DateTimeOffset.Parse("2026-10-18T22:30:00Z", CultureInfo.InvariantCulture)
                ) == "2026-10-19 01:30",
                "Payment date rolls over locally at midnight"
            );
            Check(
                PaymentDisplay.Time(
                    DateTimeOffset.Parse("2026-10-18T17:34:30+03:00", CultureInfo.InvariantCulture)
                ) == "2026-10-18 17:34",
                "An offset-bearing timestamp is not converted twice"
            );
            Check(
                PaymentDisplay.Time(null) == "—",
                "Missing payment timestamps have a neutral placeholder"
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static void Check(bool value, string label)
    {
        if (!value)
            throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }
}
