using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using HiringPs.Web.Models;

public static class AccountScheduleChecks
{
    public static async Task RunAsync(HttpClient client, string origin)
    {
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
        }
        bool Valid(object input) =>
            Validator.TryValidateObject(input, new ValidationContext(input), [], true);
        foreach (
            var (hour, period, expected) in new[]
            {
                (12, "AM", 0),
                (12, "PM", 12),
                (8, "AM", 8),
                (8, "PM", 20),
            }
        )
        {
            var clock = new SourceClock
            {
                Hour = hour,
                Minute = 35,
                Period = period,
            };
            Check(
                Valid(clock) && clock.ToTime() == new TimeOnly(expected, 35),
                "Source clock converts " + hour + " " + period + " without noon/midnight ambiguity"
            );
        }
        Check(
            !Valid(
                new SourceClock
                {
                    Hour = 0,
                    Minute = 0,
                    Period = "AM",
                }
            )
                && !Valid(
                    new SourceClock
                    {
                        Hour = 13,
                        Minute = 0,
                        Period = "PM",
                    }
                )
                && !Valid(
                    new SourceClock
                    {
                        Hour = 1,
                        Minute = 60,
                        Period = "PM",
                    }
                )
                && !Valid(
                    new SourceClock
                    {
                        Hour = 1,
                        Minute = 0,
                        Period = "other",
                    }
                )
                && !Valid(new SourceClock()),
            "Invalid or incomplete source clock cannot be stored"
        );
        Check(
            Valid(new ResetInput { Mobile = "970599123456" })
                && !Valid(new ResetInput { Mobile = "0599123456" })
                && !Valid(new ResetInput { Mobile = "someone@example.test" })
                && !Valid(new ResetInput { Mobile = "970abc599123456" })
                && !Valid(new ResetInput { Mobile = "" }),
            "Recovery accepts complete international digits and rejects email/local/malformed numbers"
        );
        foreach (var language in new[] { "ar", "en" })
        {
            var reset = await client.GetStringAsync(origin + "/" + language + "/PasswordReset");
            Check(
                reset.Contains("name=\"Mobile\"")
                    && reset.Contains("international-phone")
                    && !reset.Contains("name=\"Email\""),
                "Recovery renders a fixed plus and phone input: " + language
            );
            var token = Regex
                .Match(reset, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")
                .Groups[1]
                .Value;
            using var invalid = await client.PostAsync(
                origin + "/" + language + "/PasswordReset",
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["__RequestVerificationToken"] = token,
                        ["Mobile"] = "not-a-phone",
                    }
                )
            );
            Check(
                invalid.StatusCode == HttpStatusCode.OK
                    && (await invalid.Content.ReadAsStringAsync()).Contains("role=\"alert\""),
                "Invalid phone redisplays validation before database or Telegram access: "
                    + language
            );
            var registration = await client.GetStringAsync(
                origin + "/" + language + "/Registration"
            );
            Check(
                registration.IndexOf("id=\"telegram-mobile-help\"", StringComparison.Ordinal)
                    < registration.IndexOf("name=\"Mobile\"", StringComparison.Ordinal)
                    && registration.Contains("aria-describedby=\"telegram-mobile-help\""),
                "Telegram explanation is attached above its registration field: " + language
            );
        }
    }
}
