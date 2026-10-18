using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

[AllowAnonymous, IgnoreAntiforgeryToken]
public sealed class LahzaController(HiringDb db, IConfiguration configuration, LahzaGateway gateway)
    : Controller
{
    [HttpGet("/integrations/lahza/return")]
    public IActionResult Returned(string? reference, string? lang)
    {
        if (
            reference is null
            || !reference.StartsWith("hiring-", StringComparison.Ordinal)
            || !Guid.TryParseExact(reference[7..], "N", out var id)
        )
            return BadRequest();
        var language = LanguageUrls.IsSupported(lang) ? lang! : LanguageUrls.Select(HttpContext);
        return LocalRedirect(Request.PathBase + "/" + language + "/Member/Payments/Returned/" + id);
    }

    [HttpPost("/integrations/lahza/webhook"), RequestSizeLimit(65536)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        if (!gateway.Ready)
            return Unauthorized();
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        if (body.Length > 65536)
            return BadRequest();
        if (
            !LahzaGateway.VerifySignature(
                body.ToArray(),
                Request.Headers["x-lahza-signature"].ToString(),
                configuration["Payments:Lahza:" + gateway.Environment + ":SecretKey"] ?? ""
            )
        )
            return Unauthorized();
        try
        {
            using var json = JsonDocument.Parse(body.ToArray());
            if (
                json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("event", out var kind)
                || kind.ValueKind != JsonValueKind.String
            )
                return BadRequest();
            if (kind.GetString() != "charge.success")
                return Ok();
            if (
                !json.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("reference", out var field)
                || field.ValueKind != JsonValueKind.String
            )
                return BadRequest();
            var reference = field.GetString();
            // The signed event only schedules verification: never grant from its body.
            // Save the work before acknowledging; repeated events are harmless.
            await db
                .PaymentOrders.Where(x =>
                    x.Gateway == "Lahza"
                    && x.GatewayEnvironment == gateway.Environment
                    && x.ProviderReference == reference
                    && x.State != PaymentState.Paid
                    && x.State != PaymentState.ManuallyApproved
                )
                .ExecuteUpdateAsync(
                    update =>
                        update
                            .SetProperty(x => x.State, PaymentState.Pending)
                            .SetProperty(x => x.LastCheckedAt, (DateTimeOffset?)null),
                    ct
                );
            return Ok();
        }
        catch (JsonException)
        {
            return BadRequest();
        }
    }
}
