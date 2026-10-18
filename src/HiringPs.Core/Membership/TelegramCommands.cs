using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

public sealed class TelegramCommands(
    HiringDb db,
    TelegramClient telegram,
    MemberVerification verification,
    PaymentReconciliation payments,
    IConfiguration configuration
)
{
    public async Task HandleAsync(string body, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(body);
        if (
            !document.RootElement.TryGetProperty("message", out var message)
            || !message.TryGetProperty("from", out var sender)
            || !sender.TryGetProperty("id", out var fromId)
            || !message.TryGetProperty("chat", out var chat)
            || chat.GetProperty("type").GetString() != "private"
        )
            return;
        var from = fromId.GetInt64();
        var chatId = chat.GetProperty("id").GetInt64();
        if (from != chatId)
            return;
        if (message.TryGetProperty("contact", out var contact))
        {
            if (
                contact.TryGetProperty("user_id", out var own)
                && contact.TryGetProperty("phone_number", out var phone)
            )
            {
                try
                {
                    await verification.ContactAsync(
                        from,
                        chatId,
                        own.GetInt64(),
                        phone.GetString() ?? "",
                        ct
                    );
                }
                catch (InvalidOperationException)
                {
                    await telegram.SendAsync(
                        chatId,
                        "تعذّر التحقق من الرقم. / Cannot verify this number.",
                        ct
                    );
                }
            }
            return;
        }
        var text = message.TryGetProperty("text", out var field) ? field.GetString() ?? "" : "";
        var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (command.Length == 0)
            return;
        var name = command[0].Split('@')[0];
        if (name == "/start" && command.Length == 2)
        {
            if (!await verification.StartAsync(command[1], from, chatId, ct))
                await telegram.SendAsync(
                    chatId,
                    "رابط الربط غير صالح أو منتهٍ. افتح رابطًا جديدًا من الموقع. / This link is invalid or expired. Open a new link from the website.",
                    ct
                );
            return;
        }
        var member = await db.MemberProfiles.SingleOrDefaultAsync(
            x => x.TelegramUserId == from && x.Activated,
            ct
        );
        if (member is null)
        {
            await telegram.SendAsync(
                chatId,
                "اربط حسابك من الموقع أولًا. / Link your account from the website first.",
                ct
            );
            return;
        }
        var en = member.Language == "en";
        var origin = configuration["Seo:PublicOrigin"] ?? "https://hiring.ps";
        var site = origin.TrimEnd('/') + "/" + member.Language + "/Member";
        if (member.Suspended)
        {
            await telegram.SendAsync(
                chatId,
                (en ? "Account suspended: " : "الحساب معلّق: ") + member.SuspensionReason,
                ct
            );
            return;
        }
        switch (name)
        {
            case "/stop":
                member.NotificationsEnabled = false;
                await db.SaveChangesAsync(ct);
                await telegram.SendAsync(
                    chatId,
                    en
                        ? "Opportunity alerts stopped. Resume with a new code from your website settings. Payment and account messages remain enabled."
                        : "تم إيقاف تنبيهات الفرص. أعد تفعيلها برمز جديد من إعدادات حسابك بالموقع. تستمر رسائل الدفع والحساب.",
                    ct
                );
                break;
            case "/payment_status":
                if (command.Length != 2 || !Guid.TryParse(command[1], out var orderId))
                {
                    await telegram.SendAsync(chatId, "/payment_status orderId", ct);
                    break;
                }
                var outcome = await payments.CheckAsync(
                    orderId,
                    member.UserId,
                    "Telegram:" + member.UserId,
                    ct
                );
                // CheckAsync commits the server state before this reply is sent.
                var result = outcome switch
                {
                    "PaymentConfirmed" => en
                        ? "Payment confirmed and subscription applied."
                        : "تم تأكيد الدفع واعتماد الاشتراك.",
                    "PaymentNotFound" => en
                        ? "Order not found in your account."
                        : "لم نجد الطلب ضمن حسابك.",
                    "PaymentCheckWait" => en
                        ? "Please wait 30 seconds before checking again."
                        : "انتظر 30 ثانية قبل التحقق مجددًا.",
                    "GatewayUnavailable" => en
                        ? "Provider verification is unavailable. Request a payment review on the website."
                        : "التحقق من البوابة غير متاح. يمكنك طلب مراجعة الدفع من الموقع.",
                    _ => en ? "Payment has not been confirmed yet." : "لم يتم تأكيد الدفع بعد.",
                };
                await telegram.SendAsync(chatId, result, ct);
                break;
            case "/status":
            case "/start":
                var now = DateTimeOffset.UtcNow;
                var periods = await db
                    .MemberSubscriptions.AsNoTracking()
                    .Include(x => x.PlanVersion)
                    .Where(x => x.UserId == member.UserId && !x.Cancelled && x.EndsAt > now)
                    .OrderBy(x => x.StartsAt)
                    .ToListAsync(ct);
                var active = MembershipRules
                    .DisplayPeriods(periods)
                    .FirstOrDefault(x => x.StartsAt <= now);
                await telegram.SendAsync(
                    chatId,
                    (en ? "Plan: " : "الباقة: ")
                        + (active?.PlanVersion.Name(member.Language) ?? (en ? "Free" : "مجانية"))
                        + (
                            active is null
                                ? ""
                                : "\n"
                                    + (en ? "Ends (UTC): " : "تنتهي (UTC): ")
                                    + active.EndsAt.ToString("yyyy-MM-dd HH:mm")
                        )
                        + "\n"
                        + (en ? "Alerts: " : "التنبيهات: ")
                        + (
                            member.NotificationsEnabled
                                ? en
                                    ? "On"
                                    : "مفعلة"
                                : en
                                    ? "Off"
                                    : "متوقفة"
                        )
                        + "\n"
                        + site,
                    ct,
                    buttons: [(en ? "Open my account" : "فتح حسابي", site)]
                );
                break;
            default:
                await telegram.SendAsync(
                    chatId,
                    "/start — "
                        + (en ? "Account link and status" : "ربط الحساب وحالته")
                        + "\n/status — "
                        + (en ? "Plan and alerts" : "الباقة والتنبيهات")
                        + "\n/stop — "
                        + (en ? "Stop opportunity alerts" : "إيقاف تنبيهات الفرص")
                        + "\n/payment_status orderId — "
                        + (en ? "Check your payment" : "التحقق من الدفع")
                        + "\n/help\n"
                        + site
                        + "/Plans\n"
                        + site
                        + "/Preferences",
                    ct,
                    buttons:
                    [
                        (en ? "My plan" : "باقتي", site + "/Plans"),
                        (en ? "My preferences" : "تفضيلاتي", site + "/Preferences"),
                    ]
                );
                break;
        }
    }
}
