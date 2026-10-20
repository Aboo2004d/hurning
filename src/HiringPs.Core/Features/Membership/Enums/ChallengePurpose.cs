namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـChallengePurpose؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum ChallengePurpose
{
    Registration = 0,
    PasswordReset = 1,
    EnableNotifications = 2,
    ChangeTelegram = 3,
    AuthorizeTelegramChange = 4,
}
