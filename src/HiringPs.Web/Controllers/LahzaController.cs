using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

[AllowAnonymous, IgnoreAntiforgeryToken]
public sealed class LahzaController(
    HiringDb db,
    IConfiguration configuration,
    LahzaGateway gateway,
    LahzaWebhookService? webhookService = null
) : Controller
{
    private readonly LahzaWebhookService webhook = webhookService ?? new(db, configuration, gateway);
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
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        if (body.Length > 65536)
            return BadRequest();
        var valid = TryReadSignature(Request.Headers, out var signature, out var headerName);
        var outcome = await webhook.ProcessAsync(body.ToArray(), valid, signature, headerName, ct);
        return outcome switch
        {
            LahzaWebhookOutcome.Unauthorized => Unauthorized(),
            LahzaWebhookOutcome.InvalidRequest => BadRequest(),
            _ => Ok()
        };
    }

    /// <summary>يطبع هجاءي رأس توقيع لحظة المدعومين ويرفض القيم المتعددة أو المتعارضة.</summary>
    private static bool TryReadSignature(
        IHeaderDictionary headers,
        out string signature,
        out string headerName
    )
    {
        signature = "";
        headerName = "MissingOrAmbiguous";
        var documented = headers["x-lahza-signature"];
        var observed = headers["x_lahza_signature"];
        // HTTP header names are case-insensitive, but '-' and '_' are distinct characters.
        // Accept the documented spelling and the spelling observed in actual Lahza requests.
        if (
            documented.Count > 1
            || observed.Count > 1
            || (documented.Count == 0 && observed.Count == 0)
        )
            return false;
        if (
            documented.Count == 1
            && observed.Count == 1
            && !string.Equals(documented[0], observed[0], StringComparison.OrdinalIgnoreCase)
        )
            return false;
        signature = (documented.Count == 1 ? documented[0] : observed[0]) ?? "";
        headerName = documented.Count == 1 ? "x-lahza-signature" : "x_lahza_signature";
        return true;
    }
}
