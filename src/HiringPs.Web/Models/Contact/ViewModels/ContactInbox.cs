using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class ContactInbox
{
    public List<ContactMessageSummaryDto> Items { get; set; } = [];
    public string Status { get; set; } = "all";
    public int Count
    {
        get; set;
    }
    public int UnreadCount
    {
        get; set;
    }
    public AdminPager Pager { get; set; } = new();
}
