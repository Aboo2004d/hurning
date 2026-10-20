namespace HiringPs.Core;

/// <summary>Read-only contact message details after recording that the administrator opened it.</summary>
public sealed record ContactMessageDetailsDto(
    long Id,
    string Name,
    string Email,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsRead,
    bool IsHandled
);
