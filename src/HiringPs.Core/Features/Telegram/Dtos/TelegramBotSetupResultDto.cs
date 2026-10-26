namespace HiringPs.Core;

/// <summary>يميز قبول تسجيل Webhook عن نجاح التحقق اللاحق، دون الادعاء بأن الرسائل وصلت.</summary>
public sealed record TelegramBotSetupResultDto(bool Applied, TelegramBotStatusDto Status)
{
    public bool Verified => Applied && Status.State == TelegramBotSetupState.Registered;
}
