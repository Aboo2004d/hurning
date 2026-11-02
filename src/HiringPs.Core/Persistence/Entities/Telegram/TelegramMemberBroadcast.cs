using System.ComponentModel.DataAnnotations;
namespace HiringPs.Core;

/// <summary>Immutable audience/template snapshot, expanded in bounded batches under the membership worker lease.</summary>
public sealed class TelegramMemberBroadcast
{
    public Guid Id { get; set; }
    [MaxLength(3500)] public string Body { get; set; } = "";
    [MaxLength(200)] public string Actor { get; set; } = "";
    [MaxLength(450)] public string? UserId { get; set; }
    public bool PaidOnly { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(450)] public string? AfterUserId { get; set; }
    public bool Completed { get; set; }
}
