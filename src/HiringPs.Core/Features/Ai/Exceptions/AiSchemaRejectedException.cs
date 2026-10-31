namespace HiringPs.Core;

/// <summary>رفض مزود صريح لمخطط الرد؛ يتيح محاولة JSON متوافقة دون تخفيف فحص النتيجة.</summary>
public sealed class AiSchemaRejectedException(string message) : AiUnavailableException(message);
