namespace HiringPs.Core;

/// <summary>نتيجة حفظ المحرر بما فيها التحقق والتزامن.</summary>
public enum OpportunitySaveOutcome { Saved = 0, Invalid = 1, NotFound = 2, Conflict = 3, Duplicate = 4 }
