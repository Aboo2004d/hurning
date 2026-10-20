namespace HiringPs.Core;

/// <summary>نتيجة أمر إدارة القناة مع فصل أخطاء HTTP عن الخدمة.</summary>
public enum TelegramChannelActionOutcome { Completed = 0, InvalidRequest = 1, NotFound = 2 }
