using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

[AllowAnonymous, IgnoreAntiforgeryToken]
public class TelegramWebhookController(HiringDb db, IConfiguration configuration) : Controller
{
    [HttpPost("/integrations/telegram"), RequestSizeLimit(32768)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var secret = configuration["Telegram:WebhookSecret"] ?? "";
        var provided = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (
            !configuration.GetValue<bool>("Telegram:Enabled")
            || secret.Length < 24
            || secret.Contains('*')
            || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(secret)),
                SHA256.HashData(Encoding.UTF8.GetBytes(provided))
            )
        )
            return Unauthorized();
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        if (body.Length > 32000)
            return BadRequest();
        try
        {
            using var json = JsonDocument.Parse(body);
            if (
                !json.RootElement.TryGetProperty("update_id", out var id)
                || !id.TryGetInt64(out var update)
            )
                return BadRequest();
            if (!await db.TelegramInbox.AnyAsync(x => x.Id == update, ct))
            {
                db.TelegramInbox.Add(new TelegramInbox { Id = update, Json = body });
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                    if (!await db.TelegramInbox.AsNoTracking().AnyAsync(x => x.Id == update, ct))
                        throw;
                }
            }
            return Ok();
        }
        catch (JsonException)
        {
            return BadRequest();
        }
    }
}
