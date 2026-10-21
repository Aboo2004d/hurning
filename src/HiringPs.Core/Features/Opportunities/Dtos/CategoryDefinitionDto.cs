namespace HiringPs.Core;

/// <summary>تسمية مجال معروفة وصيغها المعتمدة للعرض ولمطابقة التنبيهات دون ترجمة تخمينية.</summary>
public sealed record CategoryDefinitionDto(string Key, string Arabic, string English, string[] Aliases);
