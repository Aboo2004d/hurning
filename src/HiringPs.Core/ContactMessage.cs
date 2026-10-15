using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

public class ContactMessage
{
    public long Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [Required, MaxLength(254)]
    public string Email { get; set; } = "";

    [Required, MaxLength(5000)]
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsRead { get; set; }
    public bool IsHandled { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}
