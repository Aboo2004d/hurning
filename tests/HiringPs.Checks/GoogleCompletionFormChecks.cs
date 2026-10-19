using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class GoogleCompletionFormChecks
{
    public static async Task RunAsync(HttpClient client, string origin)
    {
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
        }
        foreach (var language in new[] { "ar", "en" })
        {
            var path = origin + "/" + language + "/GoogleCompletionShellFixture";
            var html = await client.GetStringAsync(path);
            var form = Regex
                .Match(
                    html,
                    "<form[^>]*action=\"/" + language + "/Google/Complete\"[^>]*>.*?</form>",
                    RegexOptions.Singleline
                )
                .Value;
            var tokens = Regex.Matches(
                form,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\""
            );
            Check(
                tokens.Count == 1
                    && html.Contains("action=\"/" + language + "/Google/Complete\"")
                    && html.Contains("international-phone"),
                "Google completion posts explicitly to its action with one CSRF token and fixed phone prefix: "
                    + language
            );
            foreach (
                var phone in new[] { "970599123456", "0599123456", "+970599123456", "not-a-number" }
            )
            {
                using var posted = await client.PostAsync(
                    path + "/Validate",
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(
                                tokens[0].Groups[1].Value
                            ),
                            ["Name"] = "Fixture",
                            ["Mobile"] = phone,
                        }
                    )
                );
                using var body = JsonDocument.Parse(await posted.Content.ReadAsStringAsync());
                Check(
                    posted.StatusCode == HttpStatusCode.OK
                        && body.RootElement.GetProperty("valid").GetBoolean()
                            == (phone == "970599123456"),
                    "Google completion model binding/CSRF handles international digits and rejects malformed input: "
                        + language
                        + " "
                        + (phone == "970599123456" ? "valid" : "invalid")
                );
                if (phone == "970599123456")
                    Check(
                        body.RootElement.GetProperty("mobile").GetString() == "+970599123456",
                        "Stored phone normalization adds exactly one plus: " + language
                    );
            }
            using var missingToken = await client.PostAsync(
                path + "/Validate",
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["Name"] = "Fixture",
                        ["Mobile"] = "970599123456",
                    }
                )
            );
            Check(
                missingToken.StatusCode == HttpStatusCode.BadRequest,
                "Missing CSRF token remains rejected with 400 rather than weakening protection: "
                    + language
            );
            var rejectedHtml = await missingToken.Content.ReadAsStringAsync();
            Check(
                rejectedHtml.Contains("/" + language + "/Google/Complete")
                    && !rejectedHtml.Contains(">GoogleFormExpired<"),
                "Rejected Google form offers a localized safe GET to reopen without replaying POST: "
                    + language
            );
            using var invalidToken = await client.PostAsync(
                path + "/Validate",
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["Name"] = "Fixture",
                        ["Mobile"] = "970599123456",
                        ["__RequestVerificationToken"] = "invalid-fixture-token",
                    }
                )
            );
            Check(
                invalidToken.StatusCode == HttpStatusCode.BadRequest
                    && (await invalidToken.Content.ReadAsStringAsync()).Contains(
                        "/" + language + "/Google/Complete"
                    ),
                "Invalid Google CSRF token is also rejected and recoverable without weakening validation: "
                    + language
            );
            var duplicate = await client.GetStringAsync(path + "?duplicate=true");
            Check(
                duplicate.Contains("id=\"google-mobile-error\"")
                    && !duplicate.Contains(">TelegramMobileAlreadyRegistered<"),
                "Duplicate phone warning renders beside the field and is localized: " + language
            );
        }
    }
}
