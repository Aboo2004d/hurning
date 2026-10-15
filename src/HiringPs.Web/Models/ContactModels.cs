using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class ContactInput
{
    [Required(ErrorMessage = "ContactNameRequired"), MaxLength(120, ErrorMessage = "ContactNameLength")]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "ContactEmailRequired"), EmailAddress(ErrorMessage = "ContactEmailInvalid"), MaxLength(254, ErrorMessage = "ContactEmailInvalid")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "ContactMessageRequired"), MaxLength(5000, ErrorMessage = "ContactMessageLength")]
    public string Message { get; set; } = "";
    public string? Website { get; set; }
}

public class ContactInbox
{
    public List<ContactMessage> Items { get; set; } = [];
    public string Status { get; set; } = "all";
    public int Count { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; } = 1;
    public int Pages => Math.Max(1, (Count + 19) / 20);
}

public static class ContactReplyLinks
{
    public static string Mailto(string email, string subject) => "mailto:" + Uri.EscapeDataString(email) + "?subject=" + Uri.EscapeDataString(subject);
    public static string Gmail(string email, string subject) => "https://mail.google.com/mail/?view=cm&fs=1&to=" + Uri.EscapeDataString(email) + "&su=" + Uri.EscapeDataString(subject);
}
