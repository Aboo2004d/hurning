using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Web.Models;

public static class AdminFormChecks
{
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }

    public static async Task RunAsync(HttpClient client, string origin)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var arabic = (CultureInfo)new CultureInfo("ar").Clone();
            arabic.NumberFormat.NumberDecimalSeparator = "٫";
            arabic.NumberFormat.NumberGroupSeparator = "٬";
            CultureInfo.CurrentCulture = arabic;
            var price = typeof(PlanInput)
                .GetProperty(nameof(PlanInput.PriceIls))!
                .GetCustomAttributes(typeof(RangeAttribute), false)
                .Cast<RangeAttribute>()
                .Single();
            Check(
                price.IsValid(0.01m)
                    && price.IsValid(100000m)
                    && !price.IsValid(0m)
                    && !price.IsValid(100000.01m),
                "Decimal price limits work with Windows Arabic separators"
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        foreach (var language in new[] { "ar", "en" })
        {
            client.DefaultRequestHeaders.Add("X-Fixture-Windows-Arabic", "true");
            var html = await client.GetStringAsync(origin + "/" + language + "/Admin/Plans/Create");
            Check(
                html.Contains("/" + language + "/Admin/Plans/Create")
                    && html.Contains("name=\"PriceIls\"")
                    && html.Contains("value=\"5\""),
                "Plan creation renders with Windows Arabic number formatting: " + language
            );
            var token = Regex
                .Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")
                .Groups[1]
                .Value;
            foreach (var number in new[] { "5.75", "0", "100000.01", "invalid" })
            {
                using var posted = await client.PostAsync(
                    origin + "/" + language + "/Admin/Plans/Create",
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["__RequestVerificationToken"] = token,
                            ["NameAr"] = "",
                            ["NameEn"] = "",
                            ["PriceIls"] = number,
                            ["Days"] = "30",
                        }
                    )
                );
                var body = await posted.Content.ReadAsStringAsync();
                Check(
                    posted.StatusCode == HttpStatusCode.OK && body.Contains("validation"),
                    "Invalid plan redisplays its shared form without SQL/500: " + number
                );
                using var binding = await client.PostAsync(
                    origin + "/" + language + "/SessionShellFixture/PlanPrice",
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["__RequestVerificationToken"] = token,
                            ["PriceIls"] = number,
                        }
                    )
                );
                using var bound = JsonDocument.Parse(await binding.Content.ReadAsStringAsync());
                Check(
                    number == "5.75"
                        ? bound.RootElement.GetProperty("amount").GetDecimal() == 5.75m
                            && bound.RootElement.GetProperty("errors").GetInt32() == 0
                        : bound.RootElement.GetProperty("errors").GetInt32() > 0,
                    "Decimal binding and range validation match the actual submitted amount: "
                        + number
                );
                if (number == "5.75")
                    Check(
                        body.Contains("value=\"5.75\"") && !body.Contains("InvalidNumber"),
                        "HTML decimal amount retains its fraction in Arabic culture"
                    );
            }
            client.DefaultRequestHeaders.Remove("X-Fixture-Windows-Arabic");
            foreach (var controller in new[] { "Plans", "Discounts", "Sources", "Opportunities" })
            {
                var editing = await client.GetStringAsync(
                    origin
                        + "/"
                        + language
                        + "/Admin/AdminFormShellFixture/EditPreview?kind="
                        + controller
                );
                Check(
                    editing.Contains("/" + language + "/Admin/" + controller + "/Edit/42")
                        && !editing.Contains("/" + language + "/Admin/" + controller + "/Create"),
                    "Shared edit view posts to the record's Edit action: " + controller
                );
                foreach (var action in new[] { "Create", "Edit" })
                {
                    using var inspectedRequest = new HttpRequestMessage(
                        HttpMethod.Get,
                        origin
                            + "/"
                            + language
                            + "/Admin/"
                            + controller
                            + "/"
                            + action
                            + (action == "Edit" ? "/42" : "")
                    );
                    inspectedRequest.Headers.Add("X-Fixture-Inspect", "routes");
                    using var inspected = await client.SendAsync(inspectedRequest);
                    using var route = JsonDocument.Parse(
                        await inspected.Content.ReadAsStringAsync()
                    );
                    Check(
                        route.RootElement.GetProperty("controller").GetString() == controller
                            && route.RootElement.GetProperty("action").GetString() == action
                            && route.RootElement.GetProperty("admin").GetBoolean(),
                        "Separate protected form route resolves: " + controller + "/" + action
                    );
                    using var guarded = await client.PostAsync(
                        origin
                            + "/"
                            + language
                            + "/Admin/"
                            + controller
                            + "/"
                            + action
                            + (action == "Edit" ? "/42" : ""),
                        new FormUrlEncodedContent(
                            new Dictionary<string, string>
                            {
                                ["__RequestVerificationToken"] = token,
                                ["Id"] = "99",
                            }
                        )
                    );
                    Check(
                        guarded.StatusCode == HttpStatusCode.BadRequest,
                        "Create/Edit rejects a conflicting record ID before saving: "
                            + controller
                            + "/"
                            + action
                    );
                }
            }
            foreach (var controller in new[] { "Plans", "Sources", "Opportunities" })
            {
                var create = await client.GetStringAsync(
                    origin + "/" + language + "/Admin/" + controller + "/Create"
                );
                Check(
                    create.Contains("/" + language + "/Admin/" + controller + "/Create"),
                    "Create form explicitly submits to its own controller: " + controller
                );
            }
            using var legacy = await client.GetAsync(origin + "/" + language + "/Admin/Plans/Edit");
            Check(
                legacy.StatusCode == HttpStatusCode.Redirect
                    && legacy.Headers.Location?.ToString()
                        == "/" + language + "/Admin/Plans/Create",
                "Legacy empty Edit link redirects to Create"
            );
        }
    }
}
