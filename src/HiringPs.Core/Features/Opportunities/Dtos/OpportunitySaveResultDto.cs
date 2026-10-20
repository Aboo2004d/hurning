namespace HiringPs.Core;

/// <summary>نتيجة حفظ المحرر ورسائل التحقق دون اعتماد على MVC.</summary>
public sealed record OpportunitySaveResultDto(OpportunitySaveOutcome Outcome, List<string>? Errors = null, string? DuplicateName = null);
