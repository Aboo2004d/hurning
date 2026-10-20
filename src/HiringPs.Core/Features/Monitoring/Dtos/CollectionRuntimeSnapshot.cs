using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>DTO لنسخة حالة جولة الجمع يمكن قراءتها بالتزامن من خدمة نبضات العامل.</summary>
public record CollectionRuntimeSnapshot(
    bool Collecting,
    DateTimeOffset? LastLoopAt = null,
    DateTimeOffset? LastSuccessAt = null,
    DateTimeOffset? LastFailureAt = null,
    string ErrorType = ""
);
