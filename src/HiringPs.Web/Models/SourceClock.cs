using System.ComponentModel.DataAnnotations;

namespace HiringPs.Web.Models;

// The editor uses a 12-hour clock; the existing source schedule stores TimeOnly.
public sealed class SourceClock
{
    [Required(ErrorMessage = "FieldRequired"), Range(1, 12, ErrorMessage = "SourceClockInvalid")]
    public int? Hour { get; set; }

    [Required(ErrorMessage = "FieldRequired"), Range(0, 59, ErrorMessage = "SourceClockInvalid")]
    public int? Minute { get; set; }

    [
        Required(ErrorMessage = "FieldRequired"),
        RegularExpression("^(AM|PM)$", ErrorMessage = "SourceClockInvalid")
    ]
    public string Period { get; set; } = "";

    public TimeOnly ToTime() => new(Hour!.Value % 12 + (Period == "PM" ? 12 : 0), Minute!.Value);
}
